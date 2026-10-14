using SubiektNexoConnector.Core.Application.Parties.Shared;
using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Core.Application.Parties.GetParties;

public sealed class GetPartiesHandler
{
    private readonly IPartyReadModelReader _reader;
    private readonly IMaterializationCheckpointStore _checkpointStore;
    private readonly IMaterializationFreshnessEvaluator _freshnessEvaluator;

    public GetPartiesHandler(IPartyReadModelReader reader, IMaterializationCheckpointStore checkpointStore, IMaterializationFreshnessEvaluator freshnessEvaluator)
    {
        _reader = reader;
        _checkpointStore = checkpointStore;
        _freshnessEvaluator = freshnessEvaluator;
    }

    public async Task<MaterializedResult<IReadOnlyCollection<PartyBasicDto>?>> HandleAsync(GetPartiesQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var definition = MaterializationPolicyDefinitions.Parties;
        var checkpoint = await _checkpointStore.GetOrCreateAsync(definition.Id, cancellationToken);
        if (checkpoint.LastCycleCompletedAtUtc is null)
            return new MaterializedResult<IReadOnlyCollection<PartyBasicDto>?>(null, new MaterializationMetadata(MaterializationStatus.Unavailable, null));

        var metadata = await _freshnessEvaluator.EvaluateAsync(definition, checkpoint.LastCycleCompletedAtUtc, cancellationToken);
        var parties = await _reader.GetAllAsync(query, cancellationToken);
        return new MaterializedResult<IReadOnlyCollection<PartyBasicDto>?>(parties, metadata);
    }
}
