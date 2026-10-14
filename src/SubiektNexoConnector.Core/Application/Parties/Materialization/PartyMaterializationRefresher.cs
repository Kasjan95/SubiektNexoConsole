using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.Materialization;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Application.Parties;

public sealed class PartyMaterializationRefresher : IPartyMaterializationRefresher
{
    private readonly IPartyRepository _parties;
    private readonly IAdvancedFieldDefinitionsReadModelReader _fieldDefinitions;
    private readonly IPartyReadModelWriter _writer;

    public PartyMaterializationRefresher(IPartyRepository parties, IAdvancedFieldDefinitionsReadModelReader fieldDefinitions, IPartyReadModelWriter writer)
    {
        _parties = parties;
        _fieldDefinitions = fieldDefinitions;
        _writer = writer;
    }

    public async Task RefreshOneAsync(string signature, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signature);
        cancellationToken.ThrowIfCancellationRequested();

        var definitions = await GetDefinitionsAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var party = _parties.GetDetailsForMaterialization(signature, definitions.Values.ToArray(), cancellationToken);

        if (party is null)
        {
            await _writer.DeleteAsync(signature, cancellationToken);
            return;
        }

        await _writer.UpsertAsync(party, definitions, DateTime.UtcNow, cancellationToken);
    }

    public async Task<PartyMaterializationBatchResult> RefreshNextBatchAsync(int? afterId, int batchSize, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        cancellationToken.ThrowIfCancellationRequested();

        var definitions = await GetDefinitionsAsync(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        var sourceBatch = _parties.GetDetailsBatchAfterId(afterId, batchSize, definitions.Values.ToArray(), cancellationToken);

        if (sourceBatch.Parties.Count > 0)
            await _writer.UpsertBatchAsync(sourceBatch.Parties, definitions, DateTime.UtcNow, cancellationToken);

        return new PartyMaterializationBatchResult(sourceBatch.Parties.Count, sourceBatch.NextCursor, !sourceBatch.HasMore);
    }

    public async Task<int> CompleteCycleAsync(DateTime cycleStartedAtUtc, CancellationToken cancellationToken = default)
    {
        var removedParties = await _writer.DeleteNotRefreshedSinceAsync(cycleStartedAtUtc, cancellationToken);
        return removedParties;
    }

    private async Task<IReadOnlyDictionary<string, AdvancedFieldDefinitionDto>> GetDefinitionsAsync(CancellationToken cancellationToken)
    {
        var definitions = await _fieldDefinitions.GetAsync(AdditionalFieldTarget.Party, cancellationToken);
        return ToDefinitionDictionary(definitions);
    }

    private static IReadOnlyDictionary<string, AdvancedFieldDefinitionDto> ToDefinitionDictionary(AdvancedFieldDefinitionsDto definitions)
    {
        var groupedDefinitions = definitions.Groups.SelectMany(group => group.Fields);
        var allDefinitions = definitions.Fields.Concat(groupedDefinitions);
        return allDefinitions.ToDictionary(definition => definition.Id, StringComparer.Ordinal);
    }
}
