using NSubstitute;
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.GetAdvancedFieldDefinitions;
using SubiektNexoConnector.Core.Application.AdditionalFields.Materialization;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Core.Tests.Application.AdvancedFieldDefinitions;

public sealed class GetAdvancedFieldDefinitionsHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsDefinitionsFromMaterialization()
    {
        var materialization = Substitute.For<IAdvancedFieldDefinitionsMaterializationRefresher>();
        var query = new GetAdvancedFieldDefinitionsQuery(AdditionalFieldTarget.Product);
        var expected = new AdvancedFieldDefinitionsDto(AdditionalFieldTarget.Product, [], [new("field-id", "Color", "Display color", AdvancedFieldDataType.Text, false, true, true, true, null, null, null, null, null)]);
        var expectedResult = new MaterializedResult<AdvancedFieldDefinitionsDto>(expected, new MaterializationMetadata(MaterializationStatus.Stale, DateTime.UtcNow.AddHours(-1)));
        materialization.GetCurrentWithMetadataAsync(query.Target, Arg.Any<CancellationToken>()).Returns(expectedResult);

        var result = await new GetAdvancedFieldDefinitionsHandler(materialization).HandleAsync(query);

        Assert.Equal(expectedResult, result);
        await materialization.Received(1).GetCurrentWithMetadataAsync(query.Target, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ThrowsArgumentNullException_WhenQueryIsNull()
    {
        var materialization = Substitute.For<IAdvancedFieldDefinitionsMaterializationRefresher>();

        await Assert.ThrowsAsync<ArgumentNullException>(() => new GetAdvancedFieldDefinitionsHandler(materialization).HandleAsync(null!));
    }
}
