using Microsoft.AspNetCore.Mvc;
using SubiektNexoConnector.Core.Application.Parties.GetParties;
using SubiektNexoConnector.Core.Application.Parties.Shared;
using System.Net.Mime;

namespace SubiektNexoConnector.Api.Controllers;

[ApiController]
[Route("/[controller]")]
[Produces(MediaTypeNames.Application.Json)]
[Tags("Parties")]
public class PartiesController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<PartyBasicDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyCollection<PartyBasicDto>> GetAll(
        [FromServices] GetPartiesHandler handler,
        [FromQuery] PartyCustomerStatusFilter customerStatus = PartyCustomerStatusFilter.Standard,
        [FromQuery] short? type = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        var result = handler.Handle(new GetPartiesQuery
        {
            CustomerStatus = customerStatus,
            Type = type,
            Search = search,
            Page = page,
            PageSize = pageSize
        });
        return Ok(result);
    }
}
