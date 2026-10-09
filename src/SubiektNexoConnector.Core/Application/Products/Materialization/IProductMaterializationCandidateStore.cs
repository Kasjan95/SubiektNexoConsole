using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Core.Application.Products;

public interface IProductMaterializationCandidateStore
{
    Task<ProductMaterializationCandidateBatch> GetDueAsync(
        MaterializationPolicy policy,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<IReadOnlySet<int>> GetAllIdsAsync(
        CancellationToken cancellationToken = default);
}
