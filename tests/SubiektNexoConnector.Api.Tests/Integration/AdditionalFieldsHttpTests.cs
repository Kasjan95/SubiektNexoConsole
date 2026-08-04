using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using NSubstitute;
using SubiektNexoConnector.Core.Application.AdditionalFields.GetFieldsType;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;

namespace SubiektNexoConnector.Api.Tests.Integration;

public sealed class AdditionalFieldsHttpTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    private readonly HttpClient _client;

    public AdditionalFieldsHttpTests(TestApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateBusinessClient();
    }

    [Fact]
    public async Task GetDefinitions_Returns200AndJsonBody()
    {
        var definitions = new AdditionalFieldsDefinitionDto(
            AdditionalFieldTarget.Product,
            [
                new AdditionalFieldGroupDto(
                    "Attributes",
                    0,
                    [
                        new("field-id", "Color", "Display color", AdditionalFieldDataType.Dictionary,
                            false, true, true, true, null, null, null, null,
                            new AdditionalFieldDictionaryDto(
                                AdditionalFieldDictionaryKind.Custom,
                                "int",
                                null,
                                [new AdditionalFieldDictionaryOptionDto("1", "Red", true)]))
                    ])
            ],
            [
                new("ungrouped-field", "Quantity", null, AdditionalFieldDataType.Integer,
                    false, true, true, true, 0, null, null, null, null)
            ]);
        _factory.AdditionalFields
            .GetFieldsType(new GetFieldsTypeQuery(AdditionalFieldTarget.Product))
            .Returns(definitions);

        var response = await _client.GetAsync("/additional-fields?target=product");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        var body = await response.Content.ReadFromJsonAsync<AdditionalFieldsDefinitionDto>(serializerOptions);
        body.Should().BeEquivalentTo(definitions);
    }

    [Fact]
    public async Task GetDefinitions_Returns400_WhenTargetIsMissing()
    {
        var response = await _client.GetAsync("/additional-fields");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
