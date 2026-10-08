using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Core.Application.Products;

public sealed class ProductFreshnessPolicy : IProductFreshnessPolicy
{
    private static readonly TimeSpan HotDefaultLifetime = TimeSpan.FromSeconds(180);
    private static readonly TimeSpan ColdDefaultLifetime = TimeSpan.FromSeconds(1800);

    private readonly IMaterializationPolicyStore _materializationPolicyStore;

    public ProductFreshnessPolicy(IMaterializationPolicyStore materializationPolicyStore)
    {
        _materializationPolicyStore = materializationPolicyStore;
    }
    public async Task<DateTime> GetRefreshDueAtUtcAsync(
        ProductDetailsDto product,
        DateTime refreshedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(product);

        var isHot = product.Stocks.Sum(
            stock => stock.AvailableStock + stock.ReservedStock) > 0;

        var policyKey = isHot
            ? MaterializationPolicyKeys.ProductsHot
            : MaterializationPolicyKeys.ProductsCold;
        var defaultLifetime = isHot
            ? HotDefaultLifetime
            : ColdDefaultLifetime;

        var policy = await _materializationPolicyStore.GetOrCreateAsync(
            policyKey,
            defaultLifetime,
            cancellationToken);

        return refreshedAtUtc.Add(policy.Lifetime);
    }
}
