namespace SubiektNexoConnector.Core.Application.Parties.Addresses.DeleteAddress
{
    public sealed record DeletePartyAddressCommand
    (
        string PartySignature,
        int AddressId
    );
}
