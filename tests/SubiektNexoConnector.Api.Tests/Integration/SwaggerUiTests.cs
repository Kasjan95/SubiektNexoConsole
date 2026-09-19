using System.Text.Json;
using System.Text.RegularExpressions;
using SubiektNexoConnector.Api.Swagger;

namespace SubiektNexoConnector.Api.Tests.Integration;

public class SwaggerUiTests : IClassFixture<TestApiFactory>
{
    private readonly HttpClient _client;

    public SwaggerUiTests(TestApiFactory factory) => _client = factory.CreateBusinessClient();

    [Fact]
    public async Task Interceptor_SurvivesEmbeddingInSwaggerHtml()
    {
        var html = await _client.GetStringAsync("/swagger/index.html");
        var match = Regex.Match(html, @"var interceptors = JSON.parse\('(?<json>.*?)'\);", RegexOptions.Singleline);
        Assert.True(match.Success);
        // JSON is embedded inside a JavaScript string, so the browser decodes it twice.
        var json = Regex.Unescape(match.Groups["json"].Value);
        using var interceptors = JsonDocument.Parse(json);
        Assert.Equal(IdempotencyRequestInterceptor.Script,
            interceptors.RootElement.GetProperty("RequestInterceptorFunction").GetString());
    }
}
