using NSubstitute;
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.GetAdvancedFieldDefinitions;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;

namespace SubiektNexoConnector.Core.Tests.Application.AdvancedFieldDefinitions;

public sealed class GetAdvancedFieldDefinitionsHandlerTests
{
    [Fact]
    public void Handle_ReturnsDefinitionsFromRepository()
    {
        var repository = Substitute.For<IAdditionalFieldDefinitionRepository>();
        var query = new GetAdvancedFieldDefinitionsQuery(AdditionalFieldTarget.Product);
        var expected = new AdvancedFieldDefinitionsDto(
            AdditionalFieldTarget.Product,
            Array.Empty<AdvancedFieldGroupDto>(),
            [new("field-id", "Color", "Display color", AdvancedFieldDataType.Text, false,
                true, true, true, null, null, null, null, null)]);
        repository.GetAdvancedFieldDefinitions(query).Returns(expected);

        var result = new GetAdvancedFieldDefinitionsHandler(repository).Handle(query);

        Assert.Equal(expected, result);
        repository.Received(1).GetAdvancedFieldDefinitions(query);
    }

    [Fact]
    public void Handle_ThrowsArgumentNullException_WhenQueryIsNull()
    {
        var repository = Substitute.For<IAdditionalFieldDefinitionRepository>();

        Assert.Throws<ArgumentNullException>(() => new GetAdvancedFieldDefinitionsHandler(repository).Handle(null!));
    }
}
