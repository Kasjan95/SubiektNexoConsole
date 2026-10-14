namespace SubiektNexoConnector.Core.Materialization;

public interface IMaterializationFreshnessEvaluator
{
    Task<MaterializationMetadata> EvaluateAsync(MaterializationPolicyDefinition definition, DateTime? lastSuccessAtUtc, CancellationToken cancellationToken = default);
}
