using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SubiektNexoConnector.Api.ErrorHandling;
using SubiektNexoConnector.Infrastructure.Abstractions;
using SubiektNexoConnector.Infrastructure.Idempotency;

namespace SubiektNexoConnector.Api.Tests.ErrorHandling;

public class GlobalExceptionHandlerTests
{
    [Theory]
    [InlineData("POST", "Check the resource list")]
    [InlineData("PATCH", "Check the current resource data")]
    [InlineData("PUT", "Check the current resource data")]
    [InlineData("DELETE", "Check whether the resource still exists")]
    public async Task IndeterminateFailure_ExplainsHowToVerifyTheSpecificOperation(string method, string instruction)
    {
        var environment = Substitute.For<IHostEnvironment>();
        var logger = Substitute.For<ILogger<GlobalExceptionHandler>>();
        var service = Substitute.For<IProblemDetailsService>();
        ProblemDetailsContext? captured = null;
        service.TryWriteAsync(Arg.Do<ProblemDetailsContext>(value => captured = value))
            .Returns(new ValueTask<bool>(true));
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        await new GlobalExceptionHandler(environment, logger, service).TryHandleAsync(context,
            new IdempotencyIndeterminateException(new Exception("Uncertain result")), CancellationToken.None);
        Assert.Equal(409, context.Response.StatusCode);
        Assert.Equal("idempotency_result_indeterminate", captured!.ProblemDetails.Extensions["code"]);
        Assert.Contains(instruction, captured.ProblemDetails.Detail!);
        Assert.Contains("Do not resubmit with a new key", captured.ProblemDetails.Detail!);
    }

    [Theory]
    [InlineData(true, 503, "idempotency_storage_busy")]
    [InlineData(false, 500, "idempotency_storage_failure")]
    public async Task StorageFailure_IsMappedWithoutExposingDatabaseDetails(bool busy, int status, string code)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);
        var logger = Substitute.For<ILogger<GlobalExceptionHandler>>();
        var service = Substitute.For<IProblemDetailsService>();
        ProblemDetailsContext? captured = null;
        service.TryWriteAsync(Arg.Do<ProblemDetailsContext>(value => captured = value))
            .Returns(new ValueTask<bool>(true));
        var context = new DefaultHttpContext();
        var exception = new IdempotencyStorageException("CreateAsync", busy,
            new Exception("Sensitive database path"));
        await new GlobalExceptionHandler(environment, logger, service)
            .TryHandleAsync(context, exception, CancellationToken.None);
        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal(code, captured!.ProblemDetails.Extensions["code"]);
        Assert.DoesNotContain("Sensitive", captured.ProblemDetails.Detail!);
        Assert.Equal(busy ? "2" : "", context.Response.Headers.RetryAfter.ToString());
        Assert.Contains(logger.ReceivedCalls(), call => call.GetMethodInfo().Name == "Log"
            && Equals(call.GetArguments()[0], busy ? LogLevel.Warning : LogLevel.Error));
    }

    [Fact]
    public async Task TryHandleAsync_ReturnsBadRequestProblemDetails_ForInvalidOperationException()
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);

        var logger = Substitute.For<ILogger<GlobalExceptionHandler>>();
        var problemDetailsService = Substitute.For<IProblemDetailsService>();
        ProblemDetailsContext? capturedContext = null;

        problemDetailsService
            .TryWriteAsync(Arg.Do<ProblemDetailsContext>(context => capturedContext = context))
            .Returns(new ValueTask<bool>(true));

        var handler = new GlobalExceptionHandler(environment, logger, problemDetailsService);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Path = "/products";

        var handled = await handler.TryHandleAsync(
            httpContext,
            new InvalidOperationException("Product already exists."),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        Assert.NotNull(capturedContext);
        Assert.Equal(StatusCodes.Status400BadRequest, capturedContext!.ProblemDetails.Status);
        Assert.Equal("Bad Request", capturedContext.ProblemDetails.Title);
        Assert.Equal("Product already exists.", capturedContext.ProblemDetails.Detail);
        Assert.Equal("/products", capturedContext.ProblemDetails.Instance);
    }

    [Fact]
    public async Task TryHandleAsync_ReturnsGenericProblemDetailsOutsideDevelopment_ForUnexpectedException()
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);

        var logger = Substitute.For<ILogger<GlobalExceptionHandler>>();
        var problemDetailsService = Substitute.For<IProblemDetailsService>();
        ProblemDetailsContext? capturedContext = null;

        problemDetailsService
            .TryWriteAsync(Arg.Do<ProblemDetailsContext>(context => capturedContext = context))
            .Returns(new ValueTask<bool>(true));

        var handler = new GlobalExceptionHandler(environment, logger, problemDetailsService);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Path = "/products/ABC-123";

        var handled = await handler.TryHandleAsync(
            httpContext,
            new Exception("Sensitive failure details."),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        Assert.NotNull(capturedContext);
        Assert.Equal(StatusCodes.Status500InternalServerError, capturedContext!.ProblemDetails.Status);
        Assert.Equal("Internal Server Error", capturedContext.ProblemDetails.Title);
        Assert.Equal("The server encountered an unexpected error.", capturedContext.ProblemDetails.Detail);
        Assert.Equal("/products/ABC-123", capturedContext.ProblemDetails.Instance);
    }

    [Fact]
    public async Task TryHandleAsync_ReturnsServiceUnavailableAndRetryAfter_ForSferaQueueTimeout()
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);

        var logger = Substitute.For<ILogger<GlobalExceptionHandler>>();
        var problemDetailsService = Substitute.For<IProblemDetailsService>();
        ProblemDetailsContext? capturedContext = null;

        problemDetailsService
            .TryWriteAsync(Arg.Do<ProblemDetailsContext>(context => capturedContext = context))
            .Returns(new ValueTask<bool>(true));

        var handler = new GlobalExceptionHandler(environment, logger, problemDetailsService);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Path = "/warehouses";

        var handled = await handler.TryHandleAsync(
            httpContext,
            new SferaQueueTimeoutException(TimeSpan.FromSeconds(30), retryAfterSeconds: 5),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, httpContext.Response.StatusCode);
        Assert.Equal("5", httpContext.Response.Headers.RetryAfter);
        Assert.NotNull(capturedContext);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, capturedContext!.ProblemDetails.Status);
        Assert.Equal("Sfera is temporarily busy", capturedContext.ProblemDetails.Title);
        Assert.Equal(
            "The request was not executed because it waited too long for access to Sfera. Retry later.",
            capturedContext.ProblemDetails.Detail);
        Assert.Equal("urn:subiekt-nexo-connector:error:sfera-queue-timeout", capturedContext.ProblemDetails.Type);
        Assert.Equal("sfera_queue_timeout", capturedContext.ProblemDetails.Extensions["code"]);
        Assert.Equal(5, capturedContext.ProblemDetails.Extensions["retryAfterSeconds"]);
    }

    [Fact]
    public async Task TryHandleAsync_ReturnsExceptionMessageInDevelopment_ForUnexpectedException()
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);

        var logger = Substitute.For<ILogger<GlobalExceptionHandler>>();
        var problemDetailsService = Substitute.For<IProblemDetailsService>();
        ProblemDetailsContext? capturedContext = null;

        problemDetailsService
            .TryWriteAsync(Arg.Do<ProblemDetailsContext>(context => capturedContext = context))
            .Returns(new ValueTask<bool>(true));

        var handler = new GlobalExceptionHandler(environment, logger, problemDetailsService);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Path = "/warehouses/MAIN/products/ABC-123";

        var handled = await handler.TryHandleAsync(
            httpContext,
            new Exception("Development failure details."),
            CancellationToken.None);

        Assert.True(handled);
        Assert.NotNull(capturedContext);
        Assert.Equal(StatusCodes.Status500InternalServerError, capturedContext!.ProblemDetails.Status);
        Assert.Equal("Development failure details.", capturedContext.ProblemDetails.Detail);
    }
}
