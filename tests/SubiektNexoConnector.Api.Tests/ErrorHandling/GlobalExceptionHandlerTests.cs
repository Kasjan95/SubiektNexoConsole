using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SubiektNexoConnector.Api.ErrorHandling;

namespace SubiektNexoConnector.Api.Tests.ErrorHandling;

public class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_ReturnsGenericProblemDetailsOutsideDevelopment()
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
            new InvalidOperationException("Sensitive failure details."),
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
    public async Task TryHandleAsync_ReturnsExceptionMessageInDevelopment()
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
            new InvalidOperationException("Development failure details."),
            CancellationToken.None);

        Assert.True(handled);
        Assert.NotNull(capturedContext);
        Assert.Equal("Development failure details.", capturedContext!.ProblemDetails.Detail);
    }
}
