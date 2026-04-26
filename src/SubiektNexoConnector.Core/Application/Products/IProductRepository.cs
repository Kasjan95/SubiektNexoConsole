
namespace SubiektNexoConnector.Core.Application.Products
{
    public interface IProductRepository
    {
        IReadOnlyCollection<ProductBasicDto> GetAll();
        ProductFromWarehouseDto? GetDetailsFromWarehouse(string warehouseSymbol, string productSymbol);
        ProductDetailsDto? GetDetails(string ProductSymbol);
    }
}
