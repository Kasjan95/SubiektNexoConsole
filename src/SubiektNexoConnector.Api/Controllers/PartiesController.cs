using Microsoft.AspNetCore.Mvc;
using SubiektNexoConnector.Api.Configuration;
using SubiektNexoConnector.Api.Materialization;
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
    public async Task<ActionResult<PartyDetailsDto>> Create(
        [FromBody] CreatePartyRequestDto request,
        [FromServices] CreatePartyHandler handler,
        CancellationToken cancellationToken)
    {
        var party = await handler.HandleAsync(new CreatePartyCommand(
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
                contact.Comment)).ToArray() ?? Array.Empty<PartyContactInput>(),
            request.AdvancedFields ?? []), cancellationToken);

        return CreatedAtAction(
            nameof(GetDetails),
            new { partySignature = party.Signature },
            party);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<PartyBasicDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyCollection<PartyBasicDto>>> GetAll(
        [FromServices] GetPartiesHandler handler,
        [FromQuery] PartyCustomerStatusFilter customerStatus = PartyCustomerStatusFilter.Standard,
        [FromQuery] short? type = null,
        [FromQuery] string? search = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromServices] PaginationOptions? paginationOptions = null,
        CancellationToken cancellationToken = default)
    {
        var pagination = paginationOptions ?? new PaginationOptions();
        if (!pagination.TryResolve(page, pageSize, out var parameters, out var errors))
            return ValidationProblem(new ValidationProblemDetails(errors));

        var result = await handler.HandleAsync(new GetPartiesQuery
        {
            CustomerStatus = customerStatus,
            Type = type,
            Search = search,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        }, cancellationToken);

        HttpContext.SetMaterializationMetadata(result.Metadata);
        if (result.Value is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);

        return Ok(result.Value);
    }

    [HttpGet("create-options")]
    [ProducesResponseType(typeof(PartyCreateOptionsDto), StatusCodes.Status200OK)]
    public ActionResult<PartyCreateOptionsDto> GetCreateOptions([FromServices] GetPartyCreateOptionsHandler handler) => Ok(handler.Handle());

    [HttpGet("{partySignature}")]
    [ProducesResponseType(typeof(PartyDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PartyDetailsDto>> GetDetails(
        string partySignature,
        [FromServices] GetPartyDetailsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetPartyDetailsQuery(partySignature), cancellationToken);
        HttpContext.SetMaterializationMetadata(result.Metadata);

        if (result.Value is null)
            return NotFound();

        return Ok(result.Value);
    }

    [HttpPatch("{partySignature}")]
    [ProducesResponseType(typeof(PartyDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PartyDetailsDto>> Patch(
        string partySignature,
        [FromBody] PatchPartyRequestDto request,
        [FromServices] PatchPartyHandler handler,
        CancellationToken cancellationToken)
    {
        var party = await handler.HandleAsync(new PatchPartyCommand(
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
            request.Flag), cancellationToken);

        if (party is null)
            return NotFound();

        return Ok(party);
    }
}
