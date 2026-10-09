using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Core.Application.Products;

public interface IProductMaterializationRefresher
{
    Task RefreshOneAsync(string sku, CancellationToken cancellationToken = default);

    Task<ProductMaterializationBatchResult> RefreshNextBatchAsync(
        int? afterId,
        int batchSize,
        CancellationToken cancellationToken = default);

    Task<ProductMaterializationBatchResult> RefreshNextBatchAsync(
        MaterializationPolicyDefinition definition,
        CancellationToken cancellationToken = default);
}
