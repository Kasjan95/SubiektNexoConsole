using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace SubiektNexoConnector.Deployment.Testing;

public sealed class AdapterSmokeTester(HttpClient httpClient)
{
    public event EventHandler<string>? StagePassed;
    public async Task<AdapterSmokeTestResult> RunAsync(int port, string apiKey, TimeSpan startupTimeout,
        TimeSpan retryDelay, CancellationToken cancellationToken = default)
    {
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("API key nie może być pusty.");
        if (startupTimeout <= TimeSpan.Zero || retryDelay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(startupTimeout));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(startupTimeout);
        var health = new Uri($"http://127.0.0.1:{port}/health");
        var warehouses = new Uri($"http://127.0.0.1:{port}/warehouses");
        try
        {
            await RequestAsync(health, null, retryDelay, deadline.Token);
            StagePassed?.Invoke(this, "health");
            var body = await RequestAsync(warehouses, apiKey, retryDelay, deadline.Token);
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidOperationException("GET /warehouses nie zwrócił tablicy JSON.");
            StagePassed?.Invoke(this, "warehouses");
            return new AdapterSmokeTestResult(health, warehouses, json.RootElement.GetArrayLength());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new TimeoutException("Upłynął limit czasu uruchomienia API/Sfery. Sprawdź log adaptera i licencję, a następnie ponów sam test."); }
    }
    private async Task<string> RequestAsync(Uri uri, string? key, TimeSpan retryDelay, CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (key is not null) request.Headers.Add("X-Api-Key", key);
            using var attempt = CancellationTokenSource.CreateLinkedTokenSource(token);
            attempt.CancelAfter(TimeSpan.FromSeconds(90));
            try
            {
                using var response = await httpClient.SendAsync(request, attempt.Token);
                if (response.IsSuccessStatusCode) return await response.Content.ReadAsStringAsync(attempt.Token);
                if (response.StatusCode is not (HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout))
                    throw new HttpRequestException($"GET {uri.AbsolutePath} zwrócił HTTP {(int)response.StatusCode}. Sprawdź konfigurację, licencję i logi adaptera.", null, response.StatusCode);
                // Only known transient statuses. Do not turn an unknown 500 into five minutes of retries.
            }
            catch (HttpRequestException e) when (e.StatusCode is null) { }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
            await Task.Delay(retryDelay == TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : retryDelay, token);
        }
    }
}
