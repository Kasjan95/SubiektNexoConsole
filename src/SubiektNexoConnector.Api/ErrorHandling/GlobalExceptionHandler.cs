using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SubiektNexoConnector.Infrastructure.Abstractions;

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
        _logger.LogError(
            exception,
            "Unhandled exception while processing {Method} {Path}.",
            httpContext.Request.Method,
            httpContext.Request.Path);

        var problemDetails = CreateProblemDetails(httpContext, exception);

        if (exception is SferaQueueTimeoutException queueTimeout)
        {
            httpContext.Response.Headers.RetryAfter = queueTimeout.RetryAfterSeconds.ToString();
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
        if (exception is SferaQueueTimeoutException)
        {
            return new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Sfera is busy",
                Detail = "The request waited too long for access to Sfera. Retry later.",
                Instance = httpContext.Request.Path
            };
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
