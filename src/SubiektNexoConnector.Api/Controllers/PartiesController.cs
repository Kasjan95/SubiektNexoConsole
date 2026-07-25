using Microsoft.AspNetCore.Mvc;
using SubiektNexoConnector.Core.Application.Parties.GetParties;
using SubiektNexoConnector.Core.Application.Parties.GetPartyDetails;
using SubiektNexoConnector.Core.Application.Parties.PatchParty;
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
    [HttpGet("{partySignature}")]
    [ProducesResponseType(typeof(PartyDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<PartyDetailsDto> GetDetails(
        string partySignature,
        [FromServices] GetPartyDetailsHandler handler)
    {
        var result = handler.Handle(new GetPartyDetailsQuery(partySignature));

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpPatch("{partySignature}")]
    [ProducesResponseType(typeof(PatchPartyResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<PatchPartyResponseDto> Patch(
        string partySignature,
        [FromBody] PatchPartyRequestDto request,
        [FromServices] PatchPartyHandler handler)
    {
        var updatedSignature = handler.Handle(new PatchPartyCommand(
            partySignature,
            request.Signature,
            request.DisplayName,
            request.IsActive,
            request.FirstName,
            request.LastName,
            request.CompanyName,
            request.TaxId,
            request.EuTaxId,
            request.BusinessRegistryNumber,
            request.NationalCourtRegisterNumber,
            request.PartyGroup,
            request.Industries,
            request.Features,
            request.Notes));

        if (updatedSignature is null)
            return NotFound();

        return Ok(new PatchPartyResponseDto(updatedSignature));
    }
}
