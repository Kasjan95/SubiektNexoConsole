using FluentAssertions;
using SubiektNexoConnector.Api.Observability;

namespace SubiektNexoConnector.Api.Tests.Integration;

public class CorrelationIdHttpTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public CorrelationIdHttpTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RequestWithCorrelationId_ReturnsTheSameCorrelationIdForUnauthorizedResponse()
    {
        using var client = _factory.CreateClient();
        const string correlationId = "c02a68a5-5c9d-45f7-b77a-aacb56e93e9e";
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        var response = await client.GetAsync("/products");

        response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single()
            .Should().Be(correlationId);
    }

    [Fact]
    public async Task RequestWithoutCorrelationId_GeneratesOneForUnauthorizedResponse()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/products");

        var correlationId = response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single();
        Guid.TryParse(correlationId, out _).Should().BeTrue();
    }
}
