using System.Net;
using System.Text;
using SubiektNexoConnector.Deployment.Testing;

namespace SubiektNexoConnector.Deployment.Tests.Testing;

public sealed class AdapterSmokeTesterTests
{
    [Fact]
    public async Task RunAsync_WaitsForHealthAndTestsAuthenticatedSferaResource()
    {
        var healthCalls = 0;
        var handler = new DelegateHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/health")
            {
                healthCalls++;
                return Response(healthCalls == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK, "Healthy");
            }

            Assert.Equal("/warehouses", request.RequestUri.AbsolutePath);
            Assert.Equal("TEST-KEY", Assert.Single(request.Headers.GetValues("X-Api-Key")));
            return Response(HttpStatusCode.OK, "[{\"symbol\":\"MAG\"},{\"symbol\":\"SKL\"}]");
        });
        using var client = new HttpClient(handler);
        var passedStages = new List<string>();
        var tester = new AdapterSmokeTester(client);
        tester.StagePassed += (_, stage) => passedStages.Add(stage);

        var result = await tester.RunAsync(
            15001,
            "TEST-KEY",
            TimeSpan.FromSeconds(1),
            TimeSpan.Zero);

        Assert.Equal(2, healthCalls);
        Assert.Equal(2, result.WarehouseCount);
        Assert.Equal(["health", "warehouses"], passedStages);
    }

    [Fact]
    public async Task RunAsync_ReportsAuthenticatedResourceFailure()
    {
        var handler = new DelegateHandler(request => request.RequestUri!.AbsolutePath == "/health"
            ? Response(HttpStatusCode.OK, "Healthy")
            : Response(HttpStatusCode.Unauthorized, "invalid key"));
        using var client = new HttpClient(handler);

        var action = () => new AdapterSmokeTester(client).RunAsync(
            15001,
            "WRONG-KEY",
            TimeSpan.FromSeconds(1),
            TimeSpan.Zero);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(action);
        Assert.Contains("HTTP 401", exception.Message);
    }

    [Fact]
    public async Task RunAsync_RetriesTransientServerErrorWhileSferaWarmsUp()
    {
        var warehouseCalls = 0;
        var handler = new DelegateHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/health")
                return Response(HttpStatusCode.OK, "Healthy");

            warehouseCalls++;
            return warehouseCalls == 1
                ? Response(HttpStatusCode.ServiceUnavailable, "Sfera is warming up")
                : Response(HttpStatusCode.OK, "[{\"symbol\":\"MAG\"}]");
        });
        using var client = new HttpClient(handler);

        var result = await new AdapterSmokeTester(client).RunAsync(
            15001,
            "TEST-KEY",
            TimeSpan.FromSeconds(1),
            TimeSpan.Zero);

        Assert.Equal(2, warehouseCalls);
        Assert.Equal(1, result.WarehouseCount);
    }

    private static HttpResponseMessage Response(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(handler(request));
    }
}
