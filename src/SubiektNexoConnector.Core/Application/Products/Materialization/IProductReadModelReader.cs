using SubiektNexoConnector.Core.Application.Products;
namespace SubiektNexoConnector.Core.Application.Products;

public interface IProductReadModelReader
{
    Task<IReadOnlyCollection<ProductBasicDto>> GetAllAsync(
        GetProductsQuery query,
        CancellationToken cancellationToken = default);

    Task<MaterializedProductDetails?> GetDetailsAsync(
        string sku,
        CancellationToken cancellationToken = default);
}
