
namespace SubiektNexoConnector.Core.Application.Products
{
    public interface IProductRepository
    {
        IReadOnlyCollection<ProductBasicDto> GetAll(GetProductsQuery querry);
        ProductFromWarehouseDto? GetDetailsFromWarehouse(string warehouseSymbol, string productSymbol);
        ProductDetailsDto? GetDetails(string ProductSymbol);

        string Create(CreateProductCommand command);
        string? Patch(PatchProductCommand command);
        DeleteProductResult Delete(DeleteProductCommand command);
    }
}
