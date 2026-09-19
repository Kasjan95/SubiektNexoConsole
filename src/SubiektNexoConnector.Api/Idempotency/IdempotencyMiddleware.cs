using System.IO;
using System.Security.Claims;
using SubiektNexoConnector.Infrastructure.Idempotency;
using SubiektNexoConnector.Infrastructure.Idempotency.Persistence;

namespace SubiektNexoConnector.Api.Idempotency;

public sealed class IdempotencyMiddleware(RequestDelegate next)
{
    public const string HeaderName = "Idempotency-Key";
    public const int MaxBodyBytes = 1024 * 1024;

    public async Task InvokeAsync(
        HttpContext context,
        IdempotencyOperationStore store,
        IdempotencyExecutionContext executionContext,
        IdempotencyExecutionAccessor accessor,
        ILogger<IdempotencyMiddleware> logger)
    {
        if (!IdempotencyPolicy.Requires(context.Request.Method,
                context.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>()))
        {
            await next(context);
            return;
        }

        var clientId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (context.User.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(clientId))
        {
            await RejectAsync(context, 401, "idempotency_identity_required", "An authenticated client identity is required.");
            return;
        }

        var keys = context.Request.Headers[HeaderName];
        var key = keys.Count == 1 ? keys[0] : null;
        if (string.IsNullOrWhiteSpace(key) || key.Length > IdempotencyOperationEntity.MaxKeyLength
            || key.Any(character => character < '!' || character > '~'))
        {
            await RejectAsync(context, 400, "invalid_idempotency_key", "Provide one Idempotency-Key containing 1 to 64 visible ASCII characters.");
            return;
        }

        if (context.Request.ContentLength > MaxBodyBytes)
        {
            await RejectAsync(context, 413, "idempotency_body_too_large", "The request body exceeds the idempotency size limit.");
            return;
        }

        context.Request.EnableBuffering();
        using var body = new MemoryStream();
        var buffer = new byte[8192];
        try
        {
            context.Request.Body.Position = 0;
            int count;
            while ((count = await context.Request.Body.ReadAsync(buffer, context.RequestAborted)) > 0)
            {
                if (body.Length + count > MaxBodyBytes)
                {
                    await RejectAsync(context, 413, "idempotency_body_too_large", "The request body exceeds the idempotency size limit.");
                    return;
                }
                body.Write(buffer, 0, count);
            }
        }
        finally
        {
            context.Request.Body.Position = 0;
        }

        var target = context.Request.PathBase.Add(context.Request.Path).Value
            + context.Request.QueryString.Value;
        var result = await store.CreateAsync(clientId, key, context.Request.Method,
            target, body.ToArray(), DateTimeOffset.UtcNow, context.RequestAborted);

        if (result.Outcome == IdempotencyCreateOutcome.Conflict)
        {
            await RejectAsync(context, 409, "idempotency_key_reused", "This Idempotency-Key has already been used for a different request.");
            return;
        }

        if (result.Outcome == IdempotencyCreateOutcome.Existing)
        {
            var state = result.Operation.State;
            if (state == IdempotencyRequestState.Completed)
            {
                var saved = result.Operation;
                context.Response.StatusCode = saved.ResponseStatusCode!.Value;
                context.Response.ContentType = saved.ResponseContentType;
                if (saved.ResponseLocation is not null)
                    context.Response.Headers.Location = saved.ResponseLocation;
                context.Response.Headers["Idempotency-Replayed"] = "true";
                await context.Response.Body.WriteAsync(saved.ResponseBody!, context.RequestAborted);
                return;
            }
            var code = state switch
            {
                IdempotencyRequestState.Indeterminate => "idempotency_result_indeterminate",
                _ => "idempotency_operation_in_progress"
            };
            if (state is IdempotencyRequestState.Pending or IdempotencyRequestState.Executing)
                context.Response.Headers["Retry-After"] = "5";
            await RejectAsync(context, 409, code, state == IdempotencyRequestState.Indeterminate
                ? IdempotencyPolicy.IndeterminateDetail(context.Request.Method)
                : "The operation is in progress and will not be executed again.");
            return;
        }

        executionContext.Initialize(result.Operation.Id);
        using var lease = accessor.Begin(executionContext);
        var originalBody = context.Response.Body;
        var clientAborted = context.RequestAborted;
        using var responseBody = new MemoryStream();
        var completed = false;
        context.Response.Body = responseBody;
        try
        {
            clientAborted.ThrowIfCancellationRequested();

            // Once accepted, an idempotent write must finish and produce a durable
            // response even if the client disconnects while Sfera is executing.
            context.RequestAborted = CancellationToken.None;
            await next(context);
            var now = DateTimeOffset.UtcNow;
            if (executionContext.ExecutionStarted)
            {
                if (!await store.MarkCompletedAsync(result.Operation.Id, context.Response.StatusCode,
                    context.Response.ContentType, context.Response.Headers.Location.ToString() is { Length: > 0 } location ? location : null,
                    responseBody.ToArray(), now, now.AddHours(72)))
                    throw new InvalidOperationException("The idempotency response could not be completed.");
                completed = true;
            }
            else
            {
                // Model/command validation can finish without ever entering Sfera.
                if (!await store.ReleasePendingAsync(result.Operation.Id))
                    throw new InvalidOperationException("The unexecuted idempotency request could not be released.");
            }
        }
        catch (Exception exception)
        {
            if (!completed)
            {
                try
                {
                    if (executionContext.ExecutionStarted)
                        await store.MarkIndeterminateAsync(result.Operation.Id, DateTimeOffset.UtcNow);
                    else
                        await store.ReleasePendingAsync(result.Operation.Id);
                }
                catch (IdempotencyStorageException cleanupFailure)
                {
                    logger.LogError(cleanupFailure, "Could not finalize interrupted idempotency operation {OperationId}.", result.Operation.Id);
                }
            }
            if (executionContext.ExecutionStarted && !completed)
                throw new IdempotencyIndeterminateException(exception);
            throw;
        }
        finally
        {
            context.Response.Body = originalBody;
            context.RequestAborted = clientAborted;
        }

        // A disconnected client does not undo a completed operation: a retry replays it.
        responseBody.Position = 0;
        try
        {
            await responseBody.CopyToAsync(originalBody, clientAborted);
        }
        catch (OperationCanceledException) when (clientAborted.IsCancellationRequested)
        {
            logger.LogInformation(
                "Client disconnected after idempotency operation {OperationId} completed; the response remains available for replay.",
                result.Operation.Id);
        }
    }

    private static Task RejectAsync(HttpContext context, int status, string code, string detail) =>
        Results.Problem(statusCode: status, detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code })
            .ExecuteAsync(context);
}
