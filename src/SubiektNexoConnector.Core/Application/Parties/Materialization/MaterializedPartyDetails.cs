using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Application.Parties;

public sealed record MaterializedPartyDetails(PartyDetailsDto Party, DateTime RefreshedAtUtc);
