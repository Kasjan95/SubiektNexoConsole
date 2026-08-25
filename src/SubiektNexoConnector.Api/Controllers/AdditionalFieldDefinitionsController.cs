using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.GetAdvancedFieldDefinitions;
using SubiektNexoConnector.Core.Application.AdditionalFields.GetBasicFieldDefinitions;
using SubiektNexoConnector.Core.Application.AdditionalFields.GetFlagDefinitions;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Common;

namespace SubiektNexoConnector.Api.Controllers;

[ApiController]
[Route("/Additional-field-definitions")]
[Produces(MediaTypeNames.Application.Json)]
[Tags("Additional field definitions")]
public sealed class AdditionalFieldDefinitionsController : ControllerBase
{
    [HttpGet("advanced")]
    [ProducesResponseType(typeof(AdvancedFieldDefinitionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<AdvancedFieldDefinitionsDto> GetAdvancedDefinitions(
        [FromQuery] AdditionalFieldTarget? target,
        [FromServices] GetAdvancedFieldDefinitionsHandler handler)
    {
        if (!IsValidTarget(target))
            return InvalidTarget();

        return Ok(handler.Handle(new GetAdvancedFieldDefinitionsQuery(target.Value)));
    }

    [HttpGet("basic")]
    [ProducesResponseType(typeof(BasicFieldDefinitionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<BasicFieldDefinitionsDto> GetBasicDefinitions(
        [FromQuery] AdditionalFieldTarget? target,
        [FromServices] GetBasicFieldDefinitionsHandler handler)
    {
        if (!IsValidTarget(target))
            return InvalidTarget();

        return Ok(handler.Handle(new GetBasicFieldDefinitionsQuery(target.Value)));
    }

    [HttpGet("flags")]
    [ProducesResponseType(typeof(FlagDefinitionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<FlagDefinitionsDto> GetFlagDefinitions(
    [FromQuery] int? domain,
    [FromServices] GetFlagDefinitionHandler handler)
    {
        var domainFilter = Request.Query.ContainsKey("domain")
            ? new Optional<int?>(domain)
            : default;

        return Ok(handler.Handle(new GetFlagDefinitionQuery(domainFilter)));
    }

    private static bool IsValidTarget(AdditionalFieldTarget? target) =>
        target is not null && Enum.IsDefined(target.Value);

    private static BadRequestObjectResult InvalidTarget() => new(new ProblemDetails
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "Additional field target is required",
        Detail = "Use one of the supported targets: product or party."
    });
}
