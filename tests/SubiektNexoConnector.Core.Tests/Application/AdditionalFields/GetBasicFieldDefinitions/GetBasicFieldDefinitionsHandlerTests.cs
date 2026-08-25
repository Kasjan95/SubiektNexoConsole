using NSubstitute;
using SubiektNexoConnector.Core.Application.AdditionalFields.GetBasicFieldDefinitions;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;

namespace SubiektNexoConnector.Core.Tests.Application.AdditionalFields.GetBasicFieldDefinitions;

public sealed class GetBasicFieldDefinitionsHandlerTests
{
    [Fact]
    public void Handle_ReturnsDefinitionsFromRepository()
    {
        var repository = Substitute.For<IAdditionalFieldDefinitionRepository>();
        var query = new GetBasicFieldDefinitionsQuery(AdditionalFieldTarget.Product);
        var expected = new BasicFieldDefinitionsDto(
            AdditionalFieldTarget.Product,
            [new BasicFieldDefinitionDto("PoleWlasne1", "Długość", true)]);
        repository.GetBasicFieldDefinitions(query).Returns(expected);

        var result = new GetBasicFieldDefinitionsHandler(repository).Handle(query);

        Assert.Equal(expected, result);
        repository.Received(1).GetBasicFieldDefinitions(query);
    }

    [Fact]
    public void Handle_ThrowsArgumentNullException_WhenQueryIsNull()
    {
        var repository = Substitute.For<IAdditionalFieldDefinitionRepository>();

        Assert.Throws<ArgumentNullException>(() =>
            new GetBasicFieldDefinitionsHandler(repository).Handle(null!));
    }
}
