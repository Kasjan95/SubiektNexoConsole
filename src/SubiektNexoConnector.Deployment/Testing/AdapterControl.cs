using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace SubiektNexoConnector.Deployment.Testing;

public sealed class AdapterControl(HttpClient client)
{
    public async Task VerifyVersionAsync(int port, string apiKey, string expectedInstance, string expectedVersion,
        string expectedInformationalVersion, CancellationToken token)
    {
        await VerifyIdentityAsync(port, apiKey, expectedInstance, token);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/maintenance/info");
        request.Headers.Add("X-Api-Key", apiKey);
        using var response = await client.SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: token);
        var root = json!.RootElement;
        var actualInstance = root.TryGetProperty("instance", out var instance) ? instance.GetString() : null;
        var actualVersion = root.TryGetProperty("version", out var version) ? version.GetString() : null;
        var actualRevision = root.TryGetProperty("informationalVersion", out var revision) ? revision.GetString() : null;
        if (!string.Equals(actualInstance, expectedInstance, StringComparison.OrdinalIgnoreCase)
            || actualVersion != expectedVersion || actualRevision != expectedInformationalVersion)
            throw new InvalidOperationException(
                $"Uruchomione binaria nie są wydaniem docelowym. Oczekiwano {expectedInstance} {expectedInformationalVersion}; " +
                $"uruchomiono {actualInstance ?? "<brak>"} {actualRevision ?? actualVersion ?? "<brak wersji>"}.");
    }
    public async Task VerifyIdentityAsync(int port, string apiKey, string expectedInstance, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/maintenance/info");
        request.Headers.Add("X-Api-Key", apiKey);
        using var response = await client.SendAsync(request, token);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Adapter nie potwierdził tożsamości instancji. Starszy pilot wymaga migracji.");
        using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: token);
        if (json?.RootElement.GetProperty("instance").GetString() is not { } instance
            || !instance.Equals(expectedInstance, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Port obsługuje inną instancję adaptera.");
    }
    public async Task StopAsync(int port, string apiKey, string instance, CancellationToken token)
    {
        await VerifyIdentityAsync(port, apiKey, instance, token);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{port}/maintenance/stop");
        request.Headers.Add("X-Api-Key", apiKey);
        using var response = await client.SendAsync(request, token);
        response.EnsureSuccessStatusCode();
    }
}
