using FluentAssertions;
using SubiektNexoConnector.Api.Auth;
using System.Net;

namespace SubiektNexoConnector.Api.Tests.Integration;

public class AuthenticationHttpTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public AuthenticationHttpTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetProducts_Returns401_WhenApiKeyHeaderIsMissing()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/products");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString()
            .Should()
            .Contain(ApiKeyAuthenticationDefaults.HeaderName);
    }

    [Fact]
    public async Task GetProducts_Returns401_WhenApiKeyHeaderIsInvalid()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyAuthenticationDefaults.HeaderName, "wrong-key");

        var response = await client.GetAsync("/products");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetProducts_PassesAuthentication_WhenApiKeyHeaderIsValid()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            ApiKeyAuthenticationDefaults.HeaderName,
            TestApiFactory.ValidTestApiKey);

        var response = await client.GetAsync("/products");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }
}
