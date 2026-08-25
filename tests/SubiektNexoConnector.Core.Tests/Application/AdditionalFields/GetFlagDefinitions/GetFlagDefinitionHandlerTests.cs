using NSubstitute;
using SubiektNexoConnector.Core.Application.AdditionalFields.GetFlagDefinitions;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Common;

namespace SubiektNexoConnector.Core.Tests.Application.AdditionalFields.GetFlagDefinitions;

public sealed class GetFlagDefinitionHandlerTests
{
    [Fact]
    public void Handle_ReturnsDefinitionsFromRepository()
    {
        var repository = Substitute.For<IAdditionalFieldDefinitionRepository>();
        var query = new GetFlagDefinitionQuery(new Optional<int?>(null));
        var expected = new FlagDefinitionsDto(
            [new FlagDomainDto(null, null, [new FlagDefinitionDto(
                1, "Pilne", null, "#ff0000", "Ostrzezenie", false, true)])]);
        repository.GetFlagDefinitions(query).Returns(expected);

        var result = new GetFlagDefinitionHandler(repository).Handle(query);

        Assert.Equal(expected, result);
        repository.Received(1).GetFlagDefinitions(query);
    }

    [Fact]
    public void Handle_ThrowsArgumentNullException_WhenQueryIsNull()
    {
        var repository = Substitute.For<IAdditionalFieldDefinitionRepository>();

        Assert.Throws<ArgumentNullException>(() =>
            new GetFlagDefinitionHandler(repository).Handle(null!));
    }
}
