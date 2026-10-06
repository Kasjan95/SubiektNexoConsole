using SubiektNexoConnector.Core.Application.Products;

namespace SubiektNexoConnector.Core.Application.Products;

public interface IProductReadModelReader
{
    Task<IReadOnlyCollection<ProductBasicDto>> GetAllAsync(
        GetProductsQuery query,
        CancellationToken cancellationToken = default);

    Task<ProductDetailsDto?> GetDetailsAsync(
        string sku,
        CancellationToken cancellationToken = default);
}