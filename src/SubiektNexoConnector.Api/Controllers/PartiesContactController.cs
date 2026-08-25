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
        [ProducesResponseType(typeof(PartyContactDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<PartyContactDto> Create(
        string partySignature,
        [FromBody] CreatePartyContactRequestDto request,
        [FromServices] CreatePartyContactHandler handler)
        {
            CreatePartyContactCommand command = new CreatePartyContactCommand(partySignature, new PartyContactInput(
                request.ContactTypeId,
                request.Value,
                request.IsPrimary,
                request.Comment));
            var result = handler.Handle(command);

            if (result is null)
                return NotFound();

            return StatusCode(StatusCodes.Status201Created, result);
        }

        [HttpDelete("{contactId:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult Delete(
            string partySignature,
            int contactId,
            [FromServices] DeletePartyContactHandler handler)
        {
            if (handler.Handle(new DeletePartyContactCommand(partySignature, contactId)) == DeletePartyResourceResult.NotFound)
                return NotFound();
            return NoContent();
        }

        [HttpPatch("{contactId:int}")]
        [ProducesResponseType(typeof(PartyContactDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<PartyContactDto> Patch(
            string partySignature,
            int contactId,
            [FromBody] PatchPartyContactRequestDto request,
            [FromServices] PatchPartyContactHandler handler)
        {
            var command = new PatchPartyContactCommand(partySignature, contactId, request.IsPrimary, request.ContactValue, request.ContactDescription);
            var result = handler.Handle(command);
            if (result == null)
                return NotFound();
            return Ok(result);
        }
    }
}
