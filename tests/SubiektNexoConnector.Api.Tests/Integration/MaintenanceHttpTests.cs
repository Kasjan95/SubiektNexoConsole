using System.Net;
using SubiektNexoConnector.Api.Auth;
namespace SubiektNexoConnector.Api.Tests.Integration;
public sealed class MaintenanceHttpTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public MaintenanceHttpTests(TestApiFactory factory) => _factory = factory;
    [Theory]
    [InlineData("/maintenance/info", "GET")]
    [InlineData("/maintenance/stop", "POST")]
    public async Task MaintenanceRequiresApiKey(string path, string method)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    [Fact]
    public async Task StopRejectsUnprovenLoopback_EvenWithValidApiKey()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyAuthenticationDefaults.HeaderName, TestApiFactory.ValidTestApiKey);
        using var response = await client.PostAsync("/maintenance/stop", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
    [Fact]
    public async Task InfoDoesNotExposeSecrets()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyAuthenticationDefaults.HeaderName, TestApiFactory.ValidTestApiKey);
        using var response = await client.GetAsync("/maintenance/info");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(TestApiFactory.ValidTestApiKey, body);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        using var json = System.Text.Json.JsonDocument.Parse(body);
        var assembly = typeof(SubiektNexoConnector.Api.Auth.ApiKeyAuthenticationDefaults).Assembly;
        Assert.Equal(assembly.GetName().Version!.ToString(), json.RootElement.GetProperty("version").GetString());
        Assert.Equal(System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(assembly)!.InformationalVersion,
            json.RootElement.GetProperty("informationalVersion").GetString());
    }
}
