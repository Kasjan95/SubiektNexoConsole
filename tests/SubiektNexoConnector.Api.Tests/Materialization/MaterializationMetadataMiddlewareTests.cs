using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using SubiektNexoConnector.Api.Materialization;
using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Api.Tests.Materialization;

public sealed class MaterializationMetadataMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_AddsFreshMetadataToSuccessfulGetByDefault()
    {
        var beforeRequest = DateTime.UtcNow;
        using var response = await ExecuteAsync(HttpMethods.Get, StatusCodes.Status200OK);
        var afterRequest = DateTime.UtcNow;

        Assert.Equal("fresh", GetHeader(response, MaterializationMetadataMiddleware.StatusHeaderName));
        Assert.Equal("0", GetHeader(response, MaterializationMetadataMiddleware.AgeSecondsHeaderName));
        var lastSuccessAtUtc = DateTime.Parse(GetHeader(response, MaterializationMetadataMiddleware.LastSuccessAtHeaderName), null, System.Globalization.DateTimeStyles.RoundtripKind);
        Assert.InRange(lastSuccessAtUtc, beforeRequest, afterRequest);
    }

    [Fact]
    public async Task InvokeAsync_UsesExplicitMetadataForSuccessfulGet()
    {
        var lastSuccessAtUtc = DateTime.UtcNow.AddMinutes(-7);
        using var response = await ExecuteAsync(HttpMethods.Get, StatusCodes.Status200OK, new MaterializationMetadata(MaterializationStatus.Stale, lastSuccessAtUtc));

        Assert.Equal("stale", GetHeader(response, MaterializationMetadataMiddleware.StatusHeaderName));
        Assert.InRange(long.Parse(GetHeader(response, MaterializationMetadataMiddleware.AgeSecondsHeaderName)), 420, 425);
        Assert.Equal(lastSuccessAtUtc.ToString("O"), GetHeader(response, MaterializationMetadataMiddleware.LastSuccessAtHeaderName));
    }

    [Fact]
    public async Task InvokeAsync_WritesRefreshDueStatus()
    {
        using var response = await ExecuteAsync(HttpMethods.Get, StatusCodes.Status200OK, new MaterializationMetadata(MaterializationStatus.RefreshDue, DateTime.UtcNow));

        Assert.Equal("refresh-due", GetHeader(response, MaterializationMetadataMiddleware.StatusHeaderName));
    }

    [Fact]
    public async Task InvokeAsync_UsesLastMetadataSetByMaterializedRead()
    {
        var lastSuccessAtUtc = DateTime.UtcNow;
        using var response = await ExecuteAsync(
            HttpMethods.Get,
            StatusCodes.Status200OK,
            new MaterializationMetadata(MaterializationStatus.Stale, lastSuccessAtUtc.AddHours(-1)),
            new MaterializationMetadata(MaterializationStatus.Fresh, lastSuccessAtUtc));

        Assert.Equal("fresh", GetHeader(response, MaterializationMetadataMiddleware.StatusHeaderName));
        Assert.Equal(lastSuccessAtUtc.ToString("O"), GetHeader(response, MaterializationMetadataMiddleware.LastSuccessAtHeaderName));
    }

    [Theory]
    [InlineData(StatusCodes.Status400BadRequest)]
    [InlineData(StatusCodes.Status401Unauthorized)]
    [InlineData(StatusCodes.Status500InternalServerError)]
    public async Task InvokeAsync_DoesNotAddMetadataToUnmarkedError(int statusCode)
    {
        using var response = await ExecuteAsync(HttpMethods.Get, statusCode);

        Assert.False(response.Headers.Contains(MaterializationMetadataMiddleware.StatusHeaderName));
        Assert.False(response.Headers.Contains(MaterializationMetadataMiddleware.AgeSecondsHeaderName));
        Assert.False(response.Headers.Contains(MaterializationMetadataMiddleware.LastSuccessAtHeaderName));
    }

    [Theory]
    [InlineData(StatusCodes.Status404NotFound)]
    [InlineData(StatusCodes.Status503ServiceUnavailable)]
    public async Task InvokeAsync_AddsExplicitMetadataToControlledMaterializationResponse(int statusCode)
    {
        using var response = await ExecuteAsync(HttpMethods.Get, statusCode, new MaterializationMetadata(MaterializationStatus.Unavailable, null));

        Assert.Equal("unavailable", GetHeader(response, MaterializationMetadataMiddleware.StatusHeaderName));
        Assert.False(response.Headers.Contains(MaterializationMetadataMiddleware.AgeSecondsHeaderName));
        Assert.False(response.Headers.Contains(MaterializationMetadataMiddleware.LastSuccessAtHeaderName));
    }

    [Fact]
    public async Task InvokeAsync_DoesNotAddMetadataToNonGetRequest()
    {
        using var response = await ExecuteAsync(HttpMethods.Post, StatusCodes.Status200OK, new MaterializationMetadata(MaterializationStatus.Fresh, DateTime.UtcNow));

        Assert.False(response.Headers.Contains(MaterializationMetadataMiddleware.StatusHeaderName));
    }

    private static async Task<HttpResponseMessage> ExecuteAsync(string method, int statusCode, params MaterializationMetadata[] metadata)
    {
        var builder = new WebHostBuilder().Configure(app =>
        {
            app.UseMiddleware<MaterializationMetadataMiddleware>();
            app.Run(async context =>
            {
                context.Response.StatusCode = statusCode;
                foreach (var value in metadata)
                    context.SetMaterializationMetadata(value);

                await context.Response.WriteAsync(string.Empty);
            });
        });
        var server = new TestServer(builder);
        var client = server.CreateClient();
        var request = new HttpRequestMessage(new HttpMethod(method), "/");
        var response = await client.SendAsync(request);
        request.Dispose();
        client.Dispose();
        server.Dispose();
        return response;
    }

    private static string GetHeader(HttpResponseMessage response, string name) => response.Headers.GetValues(name).Single();
}
