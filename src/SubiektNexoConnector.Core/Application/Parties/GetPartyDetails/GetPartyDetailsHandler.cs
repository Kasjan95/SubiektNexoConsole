using SubiektNexoConnector.Core.Application.Parties.Shared;
using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Core.Application.Parties.GetPartyDetails;

public sealed class GetPartyDetailsHandler
{
    private readonly IPartyReadModelReader _reader;
    private readonly IPartyMaterializationRefresher _refresher;
    private readonly IMaterializationFreshnessEvaluator _freshnessEvaluator;

    public GetPartyDetailsHandler(IPartyReadModelReader reader, IPartyMaterializationRefresher refresher, IMaterializationFreshnessEvaluator freshnessEvaluator)
    {
        _reader = reader;
        _refresher = refresher;
        _freshnessEvaluator = freshnessEvaluator;
    }

    public async Task<MaterializedResult<PartyDetailsDto?>> HandleAsync(GetPartyDetailsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var materializedParty = await _reader.GetDetailsAsync(query.PartySignature, cancellationToken);
        if (materializedParty is not null)
        {
            var metadata = await _freshnessEvaluator.EvaluateAsync(MaterializationPolicyDefinitions.Parties, materializedParty.RefreshedAtUtc, cancellationToken);
            if (metadata.Status is MaterializationStatus.Fresh or MaterializationStatus.RefreshDue)
                return new MaterializedResult<PartyDetailsDto?>(materializedParty.Party, metadata);
        }

        try
        {
            await _refresher.RefreshOneAsync(query.PartySignature, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch when (materializedParty is not null)
        {
            return new MaterializedResult<PartyDetailsDto?>(materializedParty.Party, new MaterializationMetadata(MaterializationStatus.Stale, materializedParty.RefreshedAtUtc));
        }

        var refreshedParty = await _reader.GetDetailsAsync(query.PartySignature, cancellationToken);
        return refreshedParty is null
            ? new MaterializedResult<PartyDetailsDto?>(null, new MaterializationMetadata(MaterializationStatus.Fresh, DateTime.UtcNow))
            : new MaterializedResult<PartyDetailsDto?>(refreshedParty.Party, new MaterializationMetadata(MaterializationStatus.Fresh, refreshedParty.RefreshedAtUtc));
    }
}
