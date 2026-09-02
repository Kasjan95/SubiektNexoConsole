using FluentAssertions;
using System.Net;

namespace SubiektNexoConnector.Api.Tests.Integration;

public sealed class HealthHttpTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public HealthHttpTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHealth_ReturnsHealthyWithoutAuthentication()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }
}
