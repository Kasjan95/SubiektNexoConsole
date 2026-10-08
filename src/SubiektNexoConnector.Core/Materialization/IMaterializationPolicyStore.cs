namespace SubiektNexoConnector.Core.Materialization;
public interface IMaterializationPolicyStore
{
    Task<MaterializationPolicy> GetOrCreateAsync(
        string policyKey,
        TimeSpan defaultLifetime,
        CancellationToken cancellationToken = default);
}
