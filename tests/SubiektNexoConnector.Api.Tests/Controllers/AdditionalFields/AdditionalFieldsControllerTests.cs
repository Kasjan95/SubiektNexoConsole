using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SubiektNexoConnector.Api.Controllers;
using SubiektNexoConnector.Core.Application.AdditionalFields.GetFieldsType;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;

namespace SubiektNexoConnector.Api.Tests.Controllers;

public sealed class AdditionalFieldsControllerTests
{
    [Fact]
    public void GetDefinitions_ReturnsDefinitionsForTarget()
    {
        var repository = Substitute.For<IAdditionalFieldRepository>();
        var expected = new AdditionalFieldsDefinitionDto(
            AdditionalFieldTarget.Product,
            Array.Empty<AdditionalFieldGroupDto>(),
            [
                new("field-id", "Color", null, AdditionalFieldDataType.Text, false,
                    true, true, true, null, null, null, null, null)
            ]);
        repository.GetFieldsType(new GetFieldsTypeQuery(AdditionalFieldTarget.Product)).Returns(expected);

        var result = new AdditionalFieldsController().GetDefinitions(
            AdditionalFieldTarget.Product,
            new GetFieldsTypeHandler(repository));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expected, Assert.IsType<AdditionalFieldsDefinitionDto>(ok.Value));
    }

    [Fact]
    public void GetDefinitions_ReturnsBadRequest_WhenTargetIsMissingOrUnsupported()
    {
        var repository = Substitute.For<IAdditionalFieldRepository>();
        var controller = new AdditionalFieldsController();
        var handler = new GetFieldsTypeHandler(repository);

        Assert.IsType<BadRequestObjectResult>(controller.GetDefinitions(null, handler).Result);
        Assert.IsType<BadRequestObjectResult>(controller.GetDefinitions((AdditionalFieldTarget)99, handler).Result);
    }
}
