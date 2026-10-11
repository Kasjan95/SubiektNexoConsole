using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Core.Application.Products;

public sealed class ProductFreshnessPolicy : IProductFreshnessPolicy
{
    private readonly IMaterializationPolicyStore _materializationPolicyStore;

    public ProductFreshnessPolicy(IMaterializationPolicyStore materializationPolicyStore)
    {
        _materializationPolicyStore = materializationPolicyStore;
    }
    public async Task<MaterializationMetadata> GetMetadataAsync(ProductDetailsDto product, DateTime refreshedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(product);

        var definition = MaterializationPolicyDefinitions.ProductReadModelPolicies
            .FirstOrDefault(candidate =>
                ProductMaterializationRequirementMatcher.Matches(
                    product,
                    candidate.Requirements))
            ?? throw new InvalidOperationException(
                $"Product '{product.SKU}' does not match any materialization policy.");

        var policy = await _materializationPolicyStore.GetOrCreateAsync(
            definition,
            cancellationToken);

        return MaterializationMetadata.Evaluate(refreshedAtUtc, policy.Lifetime, policy.RefreshAhead, DateTime.UtcNow);
    }
}
