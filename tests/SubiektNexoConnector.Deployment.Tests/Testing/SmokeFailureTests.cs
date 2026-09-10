using System.Net;
using SubiektNexoConnector.Deployment.Testing;
namespace SubiektNexoConnector.Deployment.Tests.Testing;
public sealed class SmokeFailureTests
{
    [Fact]
    public async Task Unknown500FailsWithoutRetryOrLeakingBody()
    {
        var calls = 0;
        using var client = new HttpClient(new Handler((r, ct) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(r.RequestUri!.AbsolutePath == "/health" ? HttpStatusCode.OK : HttpStatusCode.InternalServerError)
                { Content = new StringContent("secret-database-connection") });
        }));
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => new AdapterSmokeTester(client).RunAsync(15001, "KEY", TimeSpan.FromSeconds(2), TimeSpan.Zero));
        Assert.Equal(2, calls); Assert.DoesNotContain("secret-database", error.Message);
    }
    [Fact]
    public async Task OverallDeadlineCancelsHangingRequest()
    {
        using var client = new HttpClient(new Handler(async (r, ct) => { await Task.Delay(Timeout.Infinite, ct); return new HttpResponseMessage(HttpStatusCode.OK); }));
        await Assert.ThrowsAsync<TimeoutException>(() => new AdapterSmokeTester(client).RunAsync(15001, "KEY", TimeSpan.FromMilliseconds(50), TimeSpan.Zero));
    }
    [Fact]
    public async Task UserCancellationRemainsCancellation()
    {
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        using var client = new HttpClient(new Handler((r, ct) => throw new Exception("must not execute")));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new AdapterSmokeTester(client).RunAsync(15001, "KEY", TimeSpan.FromSeconds(1), TimeSpan.Zero, cancel.Token));
    }
    [Fact]
    public async Task TransportFailureRetriesAuthenticatedRequest()
    {
        var count = 0;
        using var client = new HttpClient(new Handler((request, ct) =>
        {
            if (request.RequestUri!.AbsolutePath == "/health") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            Assert.Equal("KEY", request.Headers.GetValues("X-Api-Key").Single());
            if (count++ == 0) throw new HttpRequestException("connection refused");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") });
        }));
        await new AdapterSmokeTester(client).RunAsync(15001, "KEY", TimeSpan.FromSeconds(1), TimeSpan.Zero);
        Assert.Equal(2, count);
    }
    [Fact]
    public async Task IdentityMismatchPreventsShutdown()
    {
        var calls = 0;
        using var client = new HttpClient(new Handler((r, ct) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"instance\":\"different\"}") });
        }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new AdapterControl(client).StopAsync(15001, "KEY", "wanted", default));
        Assert.Equal(1, calls);
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> run) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => run(request, cancellationToken);
    }
}
