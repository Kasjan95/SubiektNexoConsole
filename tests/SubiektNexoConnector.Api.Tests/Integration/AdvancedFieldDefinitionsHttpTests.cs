using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using NSubstitute;
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.GetAdvancedFieldDefinitions;
using SubiektNexoConnector.Core.Application.AdditionalFields.GetBasicFieldDefinitions;
using SubiektNexoConnector.Core.Application.AdditionalFields.GetFlagDefinitions;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Common;

namespace SubiektNexoConnector.Api.Tests.Integration;

public sealed class AdditionalFieldDefinitionsHttpTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    private readonly HttpClient _client;

    public AdditionalFieldDefinitionsHttpTests(TestApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateBusinessClient();
    }

    [Fact]
    public async Task GetAdvancedDefinitions_Returns200AndJsonBody()
    {
        var definitions = new AdvancedFieldDefinitionsDto(
            AdditionalFieldTarget.Product,
            [],
            [new("ungrouped-field", "Quantity", null, AdvancedFieldDataType.Integer,
                false, true, true, true, 0, null, null, null, null)]);
        _factory.AdditionalFieldDefinitions
            .GetAdvancedFieldDefinitions(new GetAdvancedFieldDefinitionsQuery(AdditionalFieldTarget.Product))
            .Returns(definitions);

        var response = await _client.GetAsync("/additional-field-definitions/advanced?target=product");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        var body = await response.Content.ReadFromJsonAsync<AdvancedFieldDefinitionsDto>(serializerOptions);
        body.Should().BeEquivalentTo(definitions);
    }

    [Fact]
    public async Task GetBasicDefinitions_Returns200AndJsonBody()
    {
        var definitions = new BasicFieldDefinitionsDto(
            AdditionalFieldTarget.Product,
            [new BasicFieldDefinitionDto("PoleWlasne1", "Długość", true)]);
        _factory.AdditionalFieldDefinitions
            .GetBasicFieldDefinitions(new GetBasicFieldDefinitionsQuery(AdditionalFieldTarget.Product))
            .Returns(definitions);

        var response = await _client.GetAsync("/additional-field-definitions/basic?target=product");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        var body = await response.Content.ReadFromJsonAsync<BasicFieldDefinitionsDto>(serializerOptions);
        body.Should().BeEquivalentTo(definitions);
    }

    [Fact]
    public async Task GetFlagDefinitions_BindsAllFilterStatesAndReturnsJsonBody()
    {
        var allDefinitions = new FlagDefinitionsDto(
        [
            new FlagDomainDto(null, null, [new FlagDefinitionDto(
                1, "Pilne", null, "#ff0000", "Ostrzezenie", false, true)]),
            new FlagDomainDto(0, "Asortyment", [new FlagDefinitionDto(
                2, "Promocja", "Widoczna w sklepie", "#00ff00", "Gwiazda", true, false)])
        ]);
        var globalDefinitions = new FlagDefinitionsDto([allDefinitions.Domains.First()]);
        var productDefinitions = new FlagDefinitionsDto([allDefinitions.Domains.Last()]);

        _factory.AdditionalFieldDefinitions
            .GetFlagDefinitions(new GetFlagDefinitionQuery(default))
            .Returns(allDefinitions);
        _factory.AdditionalFieldDefinitions
            .GetFlagDefinitions(new GetFlagDefinitionQuery(new Optional<int?>(null)))
            .Returns(globalDefinitions);
        _factory.AdditionalFieldDefinitions
            .GetFlagDefinitions(new GetFlagDefinitionQuery(new Optional<int?>(0)))
            .Returns(productDefinitions);

        var allResponse = await _client.GetAsync("/additional-field-definitions/flags");
        var globalResponse = await _client.GetAsync("/additional-field-definitions/flags?domain=");
        var productResponse = await _client.GetAsync("/additional-field-definitions/flags?domain=0");

        allResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        globalResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        productResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await allResponse.Content.ReadFromJsonAsync<FlagDefinitionsDto>()).Should().BeEquivalentTo(allDefinitions);
        (await globalResponse.Content.ReadFromJsonAsync<FlagDefinitionsDto>()).Should().BeEquivalentTo(globalDefinitions);
        (await productResponse.Content.ReadFromJsonAsync<FlagDefinitionsDto>()).Should().BeEquivalentTo(productDefinitions);
    }

    [Fact]
    public async Task GetFlagDefinitions_Returns400_WhenDomainIsNotAnInteger()
    {
        var response = await _client.GetAsync("/additional-field-definitions/flags?domain=invalid");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
