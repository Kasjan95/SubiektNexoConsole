using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using SubiektNexoConnector.Core.Application.AdditionalFields.GetFieldsType;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;

namespace SubiektNexoConnector.Api.Controllers;

[ApiController]
[Route("/additional-fields")]
[Produces(MediaTypeNames.Application.Json)]
[Tags("Additional fields")]
public sealed class AdditionalFieldsController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(AdditionalFieldsDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<AdditionalFieldsDefinitionDto> GetDefinitions(
        [FromQuery] AdditionalFieldTarget? target,
        [FromServices] GetFieldsTypeHandler handler)
    {
        if (target is null || !Enum.IsDefined(target.Value))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Additional field target is required",
                Detail = "Use one of the supported targets: product or party."
            });
        }

        return Ok(handler.Handle(new GetFieldsTypeQuery(target.Value)));
    }
}
