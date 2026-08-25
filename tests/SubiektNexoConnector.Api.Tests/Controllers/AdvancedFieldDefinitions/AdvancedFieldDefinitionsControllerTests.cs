using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using SubiektNexoConnector.Api.Controllers;
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.GetAdvancedFieldDefinitions;
using SubiektNexoConnector.Core.Application.AdditionalFields.GetBasicFieldDefinitions;
using SubiektNexoConnector.Core.Application.AdditionalFields.GetFlagDefinitions;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Common;

namespace SubiektNexoConnector.Api.Tests.Controllers;

public sealed class AdditionalFieldDefinitionsControllerTests
{
    [Fact]
    public void GetAdvancedDefinitions_ReturnsDefinitionsForTarget()
    {
        var repository = Substitute.For<IAdditionalFieldDefinitionRepository>();
        var expected = new AdvancedFieldDefinitionsDto(
            AdditionalFieldTarget.Product,
            Array.Empty<AdvancedFieldGroupDto>(),
            [new("field-id", "Color", null, AdvancedFieldDataType.Text, false,
                true, true, true, null, null, null, null, null)]);
        repository.GetAdvancedFieldDefinitions(
            new GetAdvancedFieldDefinitionsQuery(AdditionalFieldTarget.Product)).Returns(expected);

        var result = new AdditionalFieldDefinitionsController().GetAdvancedDefinitions(
            AdditionalFieldTarget.Product,
            new GetAdvancedFieldDefinitionsHandler(repository));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expected, Assert.IsType<AdvancedFieldDefinitionsDto>(ok.Value));
    }

    [Fact]
    public void GetBasicDefinitions_ReturnsDefinitionsForTarget()
    {
        var repository = Substitute.For<IAdditionalFieldDefinitionRepository>();
        var expected = new BasicFieldDefinitionsDto(
            AdditionalFieldTarget.Product,
            [new BasicFieldDefinitionDto("PoleWlasne1", "Długość", true)]);
        repository.GetBasicFieldDefinitions(
            new GetBasicFieldDefinitionsQuery(AdditionalFieldTarget.Product)).Returns(expected);

        var result = new AdditionalFieldDefinitionsController().GetBasicDefinitions(
            AdditionalFieldTarget.Product,
            new GetBasicFieldDefinitionsHandler(repository));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expected, Assert.IsType<BasicFieldDefinitionsDto>(ok.Value));
    }

    [Fact]
    public void GetDefinitions_ReturnsBadRequest_WhenTargetIsMissingOrUnsupported()
    {
        var repository = Substitute.For<IAdditionalFieldDefinitionRepository>();
        var controller = new AdditionalFieldDefinitionsController();
        var advancedHandler = new GetAdvancedFieldDefinitionsHandler(repository);
        var basicHandler = new GetBasicFieldDefinitionsHandler(repository);

        Assert.IsType<BadRequestObjectResult>(controller.GetAdvancedDefinitions(null, advancedHandler).Result);
        Assert.IsType<BadRequestObjectResult>(controller.GetBasicDefinitions((AdditionalFieldTarget)99, basicHandler).Result);
    }

    [Fact]
    public void GetFlagDefinitions_ReturnsAllDomains_WhenDomainIsNotProvided()
    {
        var repository = Substitute.For<IAdditionalFieldDefinitionRepository>();
        var expected = CreateFlagDefinitions();
        repository.GetFlagDefinitions(new GetFlagDefinitionQuery(default)).Returns(expected);

        var result = CreateController().GetFlagDefinitions(
            null,
            new GetFlagDefinitionHandler(repository));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expected, Assert.IsType<FlagDefinitionsDto>(ok.Value));
        repository.Received(1).GetFlagDefinitions(new GetFlagDefinitionQuery(default));
    }

    [Fact]
    public void GetFlagDefinitions_ReturnsGlobalDomain_WhenDomainIsExplicitlyEmpty()
    {
        var repository = Substitute.For<IAdditionalFieldDefinitionRepository>();
        var query = new GetFlagDefinitionQuery(new Optional<int?>(null));
        var expected = CreateFlagDefinitions();
        repository.GetFlagDefinitions(query).Returns(expected);

        var result = CreateController("?domain=").GetFlagDefinitions(
            null,
            new GetFlagDefinitionHandler(repository));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expected, Assert.IsType<FlagDefinitionsDto>(ok.Value));
        repository.Received(1).GetFlagDefinitions(query);
    }

    [Fact]
    public void GetFlagDefinitions_ReturnsRequestedDomain_WhenDomainIsProvided()
    {
        var repository = Substitute.For<IAdditionalFieldDefinitionRepository>();
        var query = new GetFlagDefinitionQuery(new Optional<int?>(0));
        var expected = CreateFlagDefinitions();
        repository.GetFlagDefinitions(query).Returns(expected);

        var result = CreateController("?domain=0").GetFlagDefinitions(
            0,
            new GetFlagDefinitionHandler(repository));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expected, Assert.IsType<FlagDefinitionsDto>(ok.Value));
        repository.Received(1).GetFlagDefinitions(query);
    }

    private static AdditionalFieldDefinitionsController CreateController(string queryString = "")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString(queryString);

        return new AdditionalFieldDefinitionsController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private static FlagDefinitionsDto CreateFlagDefinitions() =>
        new([new FlagDomainDto(null, null, [new FlagDefinitionDto(
            1, "Pilne", null, "#ff0000", "Ostrzezenie", false, true)])]);
}
