namespace SubiektNexoConnector.Core.Application.Parties.Contacts.DeleteContact
{
    public sealed record DeletePartyContactCommand(
        string PartySignature,
        int ContactId
    );
}
