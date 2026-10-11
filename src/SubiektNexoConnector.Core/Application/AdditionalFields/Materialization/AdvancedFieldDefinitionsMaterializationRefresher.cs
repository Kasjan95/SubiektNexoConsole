using System.Text.Json;
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.GetAdvancedFieldDefinitions;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Core.Application.AdditionalFields.Materialization;

public sealed class AdvancedFieldDefinitionsMaterializationRefresher : IAdvancedFieldDefinitionsMaterializationRefresher
{
    private const int MaxErrorLength = 2000;
    private static readonly SemaphoreSlim ProductRefreshLock = new(1, 1);
    private static readonly SemaphoreSlim PartyRefreshLock = new(1, 1);
    private readonly IAdditionalFieldDefinitionRepository _source;
    private readonly IAdvancedFieldDefinitionsReadModelReader _reader;
    private readonly IAdvancedFieldDefinitionsReadModelWriter _writer;
    private readonly IMaterializationCheckpointStore _checkpointStore;
    private readonly IMaterializationPolicyStore _policyStore;

    public AdvancedFieldDefinitionsMaterializationRefresher(IAdditionalFieldDefinitionRepository source, IAdvancedFieldDefinitionsReadModelReader reader, IAdvancedFieldDefinitionsReadModelWriter writer, IMaterializationCheckpointStore checkpointStore, IMaterializationPolicyStore policyStore)
    {
        _source = source;
        _reader = reader;
        _writer = writer;
        _checkpointStore = checkpointStore;
        _policyStore = policyStore;
    }

    public async Task<AdvancedFieldDefinitionsDto> GetCurrentAsync(AdditionalFieldTarget target, CancellationToken cancellationToken = default)
    {
        var definition = MaterializationPolicyDefinitions.GetAdvancedFieldDefinitionsPolicy(target);

        try
        {
            return (await RefreshCoreAsync(target, force: false, useRefreshAhead: false, cancellationToken)).Definitions;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            var checkpoint = await _checkpointStore.GetOrCreateAsync(definition.Id, cancellationToken);
            if (checkpoint.LastCycleCompletedAtUtc is null)
                throw;

            return await _reader.GetAsync(target, cancellationToken);
        }
    }

    public async Task<MaterializedResult<AdvancedFieldDefinitionsDto>> GetCurrentWithMetadataAsync(AdditionalFieldTarget target, CancellationToken cancellationToken = default)
    {
        var definitions = await GetCurrentAsync(target, cancellationToken);
        var definition = MaterializationPolicyDefinitions.GetAdvancedFieldDefinitionsPolicy(target);
        var policy = await _policyStore.GetOrCreateAsync(definition, cancellationToken);
        var checkpoint = await _checkpointStore.GetOrCreateAsync(definition.Id, cancellationToken);
        var metadata = MaterializationMetadata.Evaluate(checkpoint.LastCycleCompletedAtUtc, policy.Lifetime, policy.RefreshAhead, DateTime.UtcNow);
        return new MaterializedResult<AdvancedFieldDefinitionsDto>(definitions, metadata);
    }

    public Task<AdvancedFieldDefinitionsMaterializationResult> RefreshIfDueAsync(AdditionalFieldTarget target, CancellationToken cancellationToken = default) => RefreshCoreAsync(target, force: false, useRefreshAhead: true, cancellationToken);

    public Task<AdvancedFieldDefinitionsMaterializationResult> RefreshAsync(AdditionalFieldTarget target, CancellationToken cancellationToken = default) => RefreshCoreAsync(target, force: true, useRefreshAhead: false, cancellationToken);

    private async Task<AdvancedFieldDefinitionsMaterializationResult> RefreshCoreAsync(AdditionalFieldTarget target, bool force, bool useRefreshAhead, CancellationToken cancellationToken)
    {
        var definition = MaterializationPolicyDefinitions.GetAdvancedFieldDefinitionsPolicy(target);
        var refreshLock = GetRefreshLock(target);
        await refreshLock.WaitAsync(cancellationToken);

        try
        {
            var policy = await _policyStore.GetOrCreateAsync(definition, cancellationToken);
            var checkpoint = await _checkpointStore.GetOrCreateAsync(definition.Id, cancellationToken);
            var current = await _reader.GetAsync(target, cancellationToken);
            var utcNow = DateTime.UtcNow;

            var refreshAhead = useRefreshAhead ? policy.RefreshAhead : TimeSpan.Zero;
            if (!force && IsFresh(checkpoint, policy, refreshAhead, utcNow))
                return new AdvancedFieldDefinitionsMaterializationResult(current, false, false);

            await _checkpointStore.SaveAsync(checkpoint with { CycleStartedAtUtc = utcNow }, cancellationToken);

            try
            {
                var source = _source.GetAdvancedFieldDefinitions(new GetAdvancedFieldDefinitionsQuery(target), cancellationToken);
                if (source.Target != target)
                    throw new InvalidOperationException($"Advanced field definitions source returned target '{source.Target}' instead of '{target}'.");

                var changed = !AreEquivalent(current, source);
                if (changed)
                    await _writer.ReplaceAsync(source, cancellationToken);

                var completedAtUtc = DateTime.UtcNow;
                await _checkpointStore.SaveAsync(checkpoint with
                {
                    NextCursor = null,
                    CycleStartedAtUtc = null,
                    LastBatchCompletedAtUtc = completedAtUtc,
                    LastCycleCompletedAtUtc = completedAtUtc,
                    LastErrorAtUtc = null,
                    LastError = null
                }, cancellationToken);

                return new AdvancedFieldDefinitionsMaterializationResult(source, true, changed);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var error = exception.ToString();
                if (error.Length > MaxErrorLength)
                    error = error[..MaxErrorLength];

                await _checkpointStore.SaveAsync(checkpoint with
                {
                    CycleStartedAtUtc = null,
                    LastErrorAtUtc = DateTime.UtcNow,
                    LastError = error
                }, cancellationToken);

                throw;
            }
        }
        finally
        {
            refreshLock.Release();
        }
    }

    private static bool IsFresh(MaterializationCheckpoint checkpoint, MaterializationPolicy policy, TimeSpan refreshAhead, DateTime utcNow) => checkpoint.LastCycleCompletedAtUtc is DateTime completedAtUtc && completedAtUtc + policy.Lifetime - refreshAhead > utcNow;

    private static bool AreEquivalent(AdvancedFieldDefinitionsDto current, AdvancedFieldDefinitionsDto source) => JsonSerializer.Serialize(current) == JsonSerializer.Serialize(source);

    private static SemaphoreSlim GetRefreshLock(AdditionalFieldTarget target) => target switch
    {
        AdditionalFieldTarget.Product => ProductRefreshLock,
        AdditionalFieldTarget.Party => PartyRefreshLock,
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported advanced field target.")
    };
}
