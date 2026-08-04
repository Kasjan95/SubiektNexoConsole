using NSubstitute;
using SubiektNexoConnector.Core.Application.AdditionalFields.GetFieldsType;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;

namespace SubiektNexoConnector.Core.Tests.Application.AdditionalFields.GetFieldsType;

public sealed class GetFieldsTypeHandlerTests
{
    [Fact]
    public void Handle_ReturnsDefinitionsFromRepository()
    {
        var repository = Substitute.For<IAdditionalFieldRepository>();
        var query = new GetFieldsTypeQuery(AdditionalFieldTarget.Product);
        var expected = new AdditionalFieldsDefinitionDto(
            AdditionalFieldTarget.Product,
            Array.Empty<AdditionalFieldGroupDto>(),
            [
                new(
                    "field-id",
                    "Color",
                    "Display color",
                    AdditionalFieldDataType.Text,
                    false,
                    true,
                    true,
                    true,
                    null,
                    null,
                    null,
                    null,
                    null)
            ]);
        repository.GetFieldsType(query).Returns(expected);

        var result = new GetFieldsTypeHandler(repository).Handle(query);

        Assert.Equal(expected, result);
        repository.Received(1).GetFieldsType(query);
    }

    [Fact]
    public void Handle_ThrowsArgumentNullException_WhenQueryIsNull()
    {
        var repository = Substitute.For<IAdditionalFieldRepository>();

        Assert.Throws<ArgumentNullException>(() => new GetFieldsTypeHandler(repository).Handle(null!));
    }
}
