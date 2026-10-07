namespace SubiektNexoConnector.Core.Materialization;

public interface IMaterializationCheckpointStore
{
    Task<MaterializationCheckpoint> GetOrCreateAsync(
        string policyKey,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        MaterializationCheckpoint checkpoint,
        CancellationToken cancellationToken = default);
}