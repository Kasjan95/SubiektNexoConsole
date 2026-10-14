using Microsoft.AspNetCore.Mvc;
using SubiektNexoConnector.Core.Application.Parties.Contacts.CreateContact;
using SubiektNexoConnector.Core.Application.Parties.Contacts.DeleteContact;
using SubiektNexoConnector.Core.Application.Parties.Contacts.PatchContact;
using SubiektNexoConnector.Core.Application.Parties.Contacts.Shared;
using SubiektNexoConnector.Core.Application.Parties.CreateParty;
using SubiektNexoConnector.Core.Application.Parties.Shared;
using System.Net.Mime;

namespace SubiektNexoConnector.Api.Controllers
{
    [ApiController]
    [Route("parties/{partySignature}/contacts")]
    [Produces(MediaTypeNames.Application.Json)]
    [Tags("Party contacts")]
    public class PartiesContactController : ControllerBase
    {
        [HttpPost]
        [ProducesResponseType(typeof(PartyDetailsDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PartyDetailsDto>> Create(
        string partySignature,
        [FromBody] CreatePartyContactRequestDto request,
        [FromServices] CreatePartyContactHandler handler,
        CancellationToken cancellationToken)
        {
            CreatePartyContactCommand command = new CreatePartyContactCommand(partySignature, new PartyContactInput(
                request.ContactTypeId,
                request.Value,
                request.IsPrimary,
                request.Comment));
            var result = await handler.HandleAsync(command, cancellationToken);

            if (result is null)
                return NotFound();

            return StatusCode(StatusCodes.Status201Created, result);
        }

        [HttpDelete("{contactId:int}")]
        [ProducesResponseType(typeof(PartyDetailsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PartyDetailsDto>> Delete(
            string partySignature,
            int contactId,
            [FromServices] DeletePartyContactHandler handler,
            CancellationToken cancellationToken)
        {
            var party = await handler.HandleAsync(new DeletePartyContactCommand(partySignature, contactId), cancellationToken);
            if (party is null)
                return NotFound();
            return Ok(party);
        }

        [HttpPatch("{contactId:int}")]
        [ProducesResponseType(typeof(PartyDetailsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PartyDetailsDto>> Patch(
            string partySignature,
            int contactId,
            [FromBody] PatchPartyContactRequestDto request,
            [FromServices] PatchPartyContactHandler handler,
            CancellationToken cancellationToken)
        {
            var command = new PatchPartyContactCommand(partySignature, contactId, request.IsPrimary, request.ContactValue, request.ContactDescription);
            var result = await handler.HandleAsync(command, cancellationToken);
            if (result == null)
                return NotFound();
            return Ok(result);
        }
    }
}
