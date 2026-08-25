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
    [ProducesResponseType(typeof(PartyAddressDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(PartyAddressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<PartyAddressDto> Create(
        string partySignature,
        [FromBody] CreatePartyAddressRequestDto request,
        [FromServices] CreatePartyAddressHandler handler)
    {
        CreatePartyAddressCommand command = new CreatePartyAddressCommand(partySignature, new PartyAddressInput(
            request.AddressTypeId,
            request.Street,
            request.HouseNumber,
            request.ApartmentNumber,
            request.PostalCode,
            request.City,
            request.CountryId));
        var result = handler.Handle(command);

        if (result is null)
            return NotFound();

        return result.IsCreated
            ? StatusCode(StatusCodes.Status201Created, result.Address)
            : Ok(result.Address);
    }

    [HttpPatch("{addressId:int}")]
    [ProducesResponseType(typeof(PartyAddressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<PartyAddressDto> Patch(
        string partySignature,
        int addressId,
        [FromBody] PatchPartyAddressRequestDto request,
        [FromServices] PatchPartyAddressHandler handler)
    {
        var result = handler.Handle(new PatchPartyAddressCommand(
            partySignature,
            addressId,
            request.Street,
            request.HouseNumber,
            request.ApartmentNumber,
            request.PostalCode,
            request.City,
            request.CountryId
        ));

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpDelete("{addressId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult Delete(
        string partySignature,
        int addressId,
        [FromServices] DeletePartyAddressHandler handler)
    {
        if (handler.Handle(new DeletePartyAddressCommand(partySignature, addressId)) == DeletePartyResourceResult.NotFound)
            return NotFound();
        return NoContent();
    }
}
