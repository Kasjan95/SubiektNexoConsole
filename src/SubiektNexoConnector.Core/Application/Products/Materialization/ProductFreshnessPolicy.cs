using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Core.Application.Products;

public sealed class ProductFreshnessPolicy : IProductFreshnessPolicy
{
    private readonly IMaterializationFreshnessEvaluator _freshnessEvaluator;

    public ProductFreshnessPolicy(IMaterializationFreshnessEvaluator freshnessEvaluator)
    {
        _freshnessEvaluator = freshnessEvaluator;
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

        return await _freshnessEvaluator.EvaluateAsync(definition, refreshedAtUtc, cancellationToken);
    }
}
