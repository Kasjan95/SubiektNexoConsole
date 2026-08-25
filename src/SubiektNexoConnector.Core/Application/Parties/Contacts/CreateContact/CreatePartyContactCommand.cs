using SubiektNexoConnector.Core.Application.Parties.CreateParty;

namespace SubiektNexoConnector.Core.Application.Parties.Contacts.CreateContact
{
    public sealed record CreatePartyContactCommand(
        string PartySignature,
        PartyContactInput Contact
    );
}
