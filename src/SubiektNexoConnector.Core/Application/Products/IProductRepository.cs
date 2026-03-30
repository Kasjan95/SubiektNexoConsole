
namespace SubiektNexoConnector.Core.Application.Products
{
    public interface IProductRepository
    {
        IReadOnlyCollection<ProductBasicDto> GetAll();
        ProductDetailsDto? GetDetails(string productSymbol, string warehouseSymbol);
    }
}
