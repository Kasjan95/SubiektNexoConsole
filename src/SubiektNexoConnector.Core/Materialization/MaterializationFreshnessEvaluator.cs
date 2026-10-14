namespace SubiektNexoConnector.Core.Materialization;

public sealed class MaterializationFreshnessEvaluator : IMaterializationFreshnessEvaluator
{
    private readonly IMaterializationPolicyStore _policyStore;

    public MaterializationFreshnessEvaluator(IMaterializationPolicyStore policyStore)
    {
        _policyStore = policyStore;
    }

    public async Task<MaterializationMetadata> EvaluateAsync(MaterializationPolicyDefinition definition, DateTime? lastSuccessAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var policy = await _policyStore.GetOrCreateAsync(definition, cancellationToken);
        return MaterializationMetadata.Evaluate(lastSuccessAtUtc, policy.Lifetime, policy.RefreshAhead, DateTime.UtcNow);
    }
}
