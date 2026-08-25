using SubiektNexoConnector.Core.Application.Parties.CreateParty;

namespace SubiektNexoConnector.Core.Application.Parties.Addresses.CreateAddress
{
    public sealed record CreatePartyAddressCommand(
        string PartySignature,
        PartyAddressInput Address);
}
