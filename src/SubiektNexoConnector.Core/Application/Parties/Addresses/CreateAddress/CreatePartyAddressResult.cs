using SubiektNexoConnector.Core.Application.Parties.Addresses.Shared;

namespace SubiektNexoConnector.Core.Application.Parties.Addresses.CreateAddress;

public sealed record CreatePartyAddressResult(
    PartyAddressDto Address,
    bool IsCreated);
