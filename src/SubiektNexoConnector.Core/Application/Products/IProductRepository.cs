
namespace SubiektNexoConnector.Core.Application.Products
{
    public interface IProductRepository
    {
        IReadOnlyCollection<ProductBasicDto> GetAll();
        ProductFromWarehouseDto? GetDetailsFromWarehouse(string ProductSymbol, string warehouseSymbol);
        ProductDetailsDto? GetDetails(string ProductSymbol);
    }
}
