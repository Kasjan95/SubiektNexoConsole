using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Application.Parties.Addresses.CreateAddress;

public sealed record CreatePartyAddressResult(
    PartyDetailsDto Party,
    bool IsCreated);
