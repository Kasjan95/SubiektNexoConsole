using SubiektNexoConnector.Core.Materialization;
using SubiektNexoConnector.Infrastructure.Materialization.Persistence;

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence.States;

internal sealed class MaterializationCheckpointStore : IMaterializationCheckpointStore
{
    private readonly ReadModelDbContext _dbContext;

    public MaterializationCheckpointStore(ReadModelDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MaterializationCheckpoint> GetOrCreateAsync(
        string policyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyKey);

        var entity = await GetOrCreateEntityAsync(policyKey, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToCheckpoint(entity);
    }

    public async Task SaveAsync(
        MaterializationCheckpoint checkpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpoint.PolicyKey);

        var entity = await GetOrCreateEntityAsync(
            checkpoint.PolicyKey,
            cancellationToken);

        entity.NextCursor = checkpoint.NextCursor;
        entity.CycleStartedAtUtc = checkpoint.CycleStartedAtUtc;
        entity.LastBatchCompletedAtUtc = checkpoint.LastBatchCompletedAtUtc;
        entity.LastCycleCompletedAtUtc = checkpoint.LastCycleCompletedAtUtc;
        entity.LastErrorAtUtc = checkpoint.LastErrorAtUtc;
        entity.LastError = checkpoint.LastError;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<MaterializationStateEntity> GetOrCreateEntityAsync(
        string policyKey,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.MaterializationStates.FindAsync(
            [policyKey],
            cancellationToken);

        if (entity is not null)
            return entity;

        entity = new MaterializationStateEntity
        {
            PolicyKey = policyKey
        };

        _dbContext.MaterializationStates.Add(entity);
        return entity;
    }

    private static MaterializationCheckpoint ToCheckpoint(
        MaterializationStateEntity entity) => new(
        entity.PolicyKey,
        entity.NextCursor,
        entity.CycleStartedAtUtc,
        entity.LastBatchCompletedAtUtc,
        entity.LastCycleCompletedAtUtc,
        entity.LastErrorAtUtc,
        entity.LastError);
}
