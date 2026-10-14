using Microsoft.AspNetCore.Mvc;
using SubiektNexoConnector.Core.Application.Parties.Addresses.CreateAddress;
using SubiektNexoConnector.Core.Application.Parties.Addresses.PatchAddress;
using SubiektNexoConnector.Core.Application.Parties.Addresses.DeleteAddress;
using SubiektNexoConnector.Core.Application.Parties.Addresses.Shared;
using SubiektNexoConnector.Core.Application.Parties.CreateParty;
using System.Net.Mime;
using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Api.Controllers;

[ApiController]
[Route("parties/{partySignature}/addresses")]
[Produces(MediaTypeNames.Application.Json)]
[Tags("Party addresses")]
public class PartiesAddressesControler : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(PartyDetailsDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(PartyDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PartyDetailsDto>> Create(
        string partySignature,
        [FromBody] CreatePartyAddressRequestDto request,
        [FromServices] CreatePartyAddressHandler handler,
        CancellationToken cancellationToken)
    {
        CreatePartyAddressCommand command = new CreatePartyAddressCommand(partySignature, new PartyAddressInput(
            request.AddressTypeId,
            request.Street,
            request.HouseNumber,
            request.ApartmentNumber,
            request.PostalCode,
            request.City,
            request.CountryId));
        var result = await handler.HandleAsync(command, cancellationToken);

        if (result is null)
            return NotFound();

        return result.IsCreated
            ? StatusCode(StatusCodes.Status201Created, result.Party)
            : Ok(result.Party);
    }

    [HttpPatch("{addressId:int}")]
    [ProducesResponseType(typeof(PartyDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PartyDetailsDto>> Patch(
        string partySignature,
        int addressId,
        [FromBody] PatchPartyAddressRequestDto request,
        [FromServices] PatchPartyAddressHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new PatchPartyAddressCommand(
            partySignature,
            addressId,
            request.Street,
            request.HouseNumber,
            request.ApartmentNumber,
            request.PostalCode,
            request.City,
            request.CountryId
        ), cancellationToken);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpDelete("{addressId:int}")]
    [ProducesResponseType(typeof(PartyDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PartyDetailsDto>> Delete(
        string partySignature,
        int addressId,
        [FromServices] DeletePartyAddressHandler handler,
        CancellationToken cancellationToken)
    {
        var party = await handler.HandleAsync(new DeletePartyAddressCommand(partySignature, addressId), cancellationToken);
        if (party is null)
            return NotFound();
        return Ok(party);
    }
}
