namespace SubiektNexoConnector.Core.Materialization;
public interface IMaterializationPolicyStore
{
    Task<MaterializationPolicy> GetOrCreateAsync(
        MaterializationPolicyDefinition definition,
        CancellationToken cancellationToken = default);
}
