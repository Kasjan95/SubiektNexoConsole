using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Application.Parties;

public sealed record PartyMaterializationSourceBatch(IReadOnlyCollection<PartyDetailsDto> Parties, int? NextCursor, bool HasMore);
