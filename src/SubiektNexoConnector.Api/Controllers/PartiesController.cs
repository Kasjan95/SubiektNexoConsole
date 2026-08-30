using Microsoft.AspNetCore.Mvc;
using SubiektNexoConnector.Api.Configuration;
using SubiektNexoConnector.Core.Application.Parties.CreateParty;
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
    [HttpPost]
    [ProducesResponseType(typeof(PartyDetailsDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<PartyDetailsDto> Create(
        [FromBody] CreatePartyRequestDto request,
        [FromServices] CreatePartyHandler handler)
    {
        var party = handler.Handle(new CreatePartyCommand(
            request.DisplayName,
            request.Type!.Value,
            request.Subtype!.Value,
            request.Signature,
            request.FirstName,
            request.LastName,
            request.CompanyName,
            request.TaxId,
            request.EuTaxId,
            request.BusinessRegistryNumber,
            request.NationalCourtRegisterNumber,
            request.PartyGroupId,
            request.IndustryIds ?? Array.Empty<int>(),
            request.FeatureIds ?? Array.Empty<int>(),
            request.Notes,
            request.Addresses?.Select(address => new PartyAddressInput(
                address.AddressTypeId,
                address.Street,
                address.HouseNumber,
                address.ApartmentNumber,
                address.PostalCode,
                address.City,
                address.CountryId)).ToArray() ?? Array.Empty<PartyAddressInput>(),
            request.Contacts?.Select(contact => new PartyContactInput(
                contact.ContactTypeId,
                contact.Value,
                contact.IsPrimary,
                contact.Comment)).ToArray() ?? Array.Empty<PartyContactInput>()));

        return CreatedAtAction(
            nameof(GetDetails),
            new { partySignature = party.Signature },
            party);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<PartyBasicDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<IReadOnlyCollection<PartyBasicDto>> GetAll(
        [FromServices] GetPartiesHandler handler,
        [FromQuery] PartyCustomerStatusFilter customerStatus = PartyCustomerStatusFilter.Standard,
        [FromQuery] short? type = null,
        [FromQuery] string? search = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromServices] PaginationOptions? paginationOptions = null)
    {
        var pagination = paginationOptions ?? new PaginationOptions();
        if (!pagination.TryResolve(page, pageSize, out var parameters, out var errors))
            return ValidationProblem(new ValidationProblemDetails(errors));

        var result = handler.Handle(new GetPartiesQuery
        {
            CustomerStatus = customerStatus,
            Type = type,
            Search = search,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        });
        return Ok(result);
    }

    [HttpGet("create-options")]
    [ProducesResponseType(typeof(PartyCreateOptionsDto), StatusCodes.Status200OK)]
    public ActionResult<PartyCreateOptionsDto> GetCreateOptions([FromServices] GetPartyCreateOptionsHandler handler) => Ok(handler.Handle());

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
            request.PartyGroupId,
            request.IndustryIds,
            request.FeatureIds,
            request.Notes,
            request.BasicFields,
            request.AdvancedFields,
            request.Flag));

        if (updatedSignature is null)
            return NotFound();

        return Ok(new PatchPartyResponseDto(updatedSignature));
    }
}
