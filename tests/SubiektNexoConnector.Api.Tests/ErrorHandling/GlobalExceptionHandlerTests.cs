using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SubiektNexoConnector.Api.ErrorHandling;
using SubiektNexoConnector.Infrastructure.Abstractions;

namespace SubiektNexoConnector.Api.Tests.ErrorHandling;

public class GlobalExceptionHandlerTests
{
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
