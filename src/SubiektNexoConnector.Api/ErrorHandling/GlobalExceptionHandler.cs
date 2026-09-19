using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SubiektNexoConnector.Api.Idempotency;
using SubiektNexoConnector.Infrastructure.Abstractions;
using SubiektNexoConnector.Infrastructure.Idempotency;

namespace SubiektNexoConnector.Api.ErrorHandling;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IHostEnvironment _environment;
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IProblemDetailsService _problemDetailsService;

    public GlobalExceptionHandler(
        IHostEnvironment environment,
        ILogger<GlobalExceptionHandler> logger,
        IProblemDetailsService problemDetailsService)
    {
        _environment = environment;
        _logger = logger;
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is IdempotencyStorageException { IsBusy: true } storageBusy)
        {
            _logger.LogWarning(exception,
                "Idempotency storage remained locked during {StorageOperation} for {Method} {Path}.",
                storageBusy.OperationName, httpContext.Request.Method, httpContext.Request.Path);
        }
        else if (exception is SferaQueueTimeoutException queueTimeout)
        {
            _logger.LogWarning(
                "Request {Method} {Path} timed out while waiting for Sfera. Retry after {RetryAfterSeconds} seconds.",
                httpContext.Request.Method,
                httpContext.Request.Path,
                queueTimeout.RetryAfterSeconds);
        }
        else
        {
            _logger.LogError(
                exception,
                "Unhandled exception while processing {Method} {Path}.",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }

        var problemDetails = CreateProblemDetails(httpContext, exception);
        if (exception is IdempotencyStorageException { IsBusy: true })
            httpContext.Response.Headers.RetryAfter = "2";

        if (exception is SferaQueueTimeoutException timeoutException)
        {
            httpContext.Response.Headers.RetryAfter = timeoutException.RetryAfterSeconds.ToString();
        }

        httpContext.Response.StatusCode = problemDetails.Status!.Value;

        var wasWritten = await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });

        if (wasWritten)
            return true;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private ProblemDetails CreateProblemDetails(HttpContext httpContext, Exception exception)
    {
        if (exception is IdempotencyIndeterminateException)
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Operation result is uncertain",
                Detail = IdempotencyPolicy.IndeterminateDetail(httpContext.Request.Method),
                Instance = httpContext.Request.Path
            };
            problem.Extensions["code"] = "idempotency_result_indeterminate";
            return problem;
        }

        if (exception is IdempotencyStorageException storage)
        {
            var problem = new ProblemDetails
            {
                Status = storage.IsBusy ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status500InternalServerError,
                Title = storage.IsBusy ? "Idempotency storage is temporarily busy" : "Idempotency storage failure",
                Detail = "The idempotency storage operation could not be confirmed. Do not submit this operation with a new idempotency key.",
                Instance = httpContext.Request.Path,
                Type = "urn:subiekt-nexo-connector:error:idempotency-storage"
            };
            problem.Extensions["code"] = storage.IsBusy ? "idempotency_storage_busy" : "idempotency_storage_failure";
            return problem;
        }

        if (exception is SferaQueueTimeoutException queueTimeout)
        {
            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Sfera is temporarily busy",
                Detail = "The request was not executed because it waited too long for access to Sfera. Retry later.",
                Instance = httpContext.Request.Path,
                Type = "urn:subiekt-nexo-connector:error:sfera-queue-timeout"
            };

            problemDetails.Extensions["code"] = "sfera_queue_timeout";
            problemDetails.Extensions["retryAfterSeconds"] = queueTimeout.RetryAfterSeconds;

            return problemDetails;
        }

        if (exception is InvalidOperationException)
        {
            return new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = exception.Message,
                Instance = httpContext.Request.Path
            };
        }

        return new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Internal Server Error",
            Detail = _environment.IsDevelopment()
                ? exception.Message
                : "The server encountered an unexpected error.",
            Instance = httpContext.Request.Path
        };
    }
}
