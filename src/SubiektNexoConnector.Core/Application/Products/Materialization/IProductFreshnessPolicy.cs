using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Core.Application.Products;

public interface IProductFreshnessPolicy
{
    Task<MaterializationMetadata> GetMetadataAsync(ProductDetailsDto product, DateTime refreshedAtUtc, CancellationToken cancellationToken = default);
}
