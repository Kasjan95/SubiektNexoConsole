using Microsoft.AspNetCore.Http;
using SubiektNexoConnector.Api.Observability;

namespace SubiektNexoConnector.Api.Tests.Observability;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_PreservesAndNormalizesValidCorrelationId()
    {
        var receivedCorrelationId = string.Empty;
        var middleware = new CorrelationIdMiddleware(context =>
        {
            receivedCorrelationId = context.Items[CorrelationIdMiddleware.HttpContextItemKey] as string
                ?? throw new InvalidOperationException("Correlation ID was not stored in the HTTP context.");
            return Task.CompletedTask;
        });
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName] = "C02A68A5-5C9D-45F7-B77A-AACB56E93E9E";

        await middleware.InvokeAsync(httpContext);

        const string expected = "c02a68a5-5c9d-45f7-b77a-aacb56e93e9e";
        Assert.Equal(expected, receivedCorrelationId);
        Assert.Equal(expected, httpContext.Response.Headers[CorrelationIdMiddleware.HeaderName]);
    }

    [Fact]
    public async Task InvokeAsync_GeneratesCorrelationIdWhenHeaderIsMissing()
    {
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        var httpContext = new DefaultHttpContext();

        await middleware.InvokeAsync(httpContext);

        var correlationId = httpContext.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        Assert.True(Guid.TryParse(correlationId, out _));
        Assert.Equal(correlationId, httpContext.Items[CorrelationIdMiddleware.HttpContextItemKey]);
    }

    [Fact]
    public async Task InvokeAsync_GeneratesCorrelationIdWhenHeaderIsInvalid()
    {
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName] = "not-a-guid";

        await middleware.InvokeAsync(httpContext);

        var correlationId = httpContext.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        Assert.True(Guid.TryParse(correlationId, out _));
        Assert.NotEqual("not-a-guid", correlationId);
    }
}
