using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Application.Parties;

public interface IPartyReadModelWriter
{
    Task UpsertAsync(PartyDetailsDto party, IReadOnlyDictionary<string, AdvancedFieldDefinitionDto> advancedFieldDefinitions, DateTime refreshedAtUtc, CancellationToken cancellationToken = default);
    Task UpsertBatchAsync(IReadOnlyCollection<PartyDetailsDto> parties, IReadOnlyDictionary<string, AdvancedFieldDefinitionDto> advancedFieldDefinitions, DateTime refreshedAtUtc, CancellationToken cancellationToken = default);
    Task DeleteAsync(string signature, CancellationToken cancellationToken = default);
    Task<int> DeleteNotRefreshedSinceAsync(DateTime cycleStartedAtUtc, CancellationToken cancellationToken = default);
}
