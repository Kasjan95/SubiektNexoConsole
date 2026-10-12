namespace SubiektNexoConnector.Core.Application.Products;

public interface IProductReadModelSynchronizer
{
    Task<bool> TryUpsertAsync(ProductDetailsDto product, CancellationToken cancellationToken = default);
    Task<bool> TryDeleteAsync(string sku, CancellationToken cancellationToken = default);
}
