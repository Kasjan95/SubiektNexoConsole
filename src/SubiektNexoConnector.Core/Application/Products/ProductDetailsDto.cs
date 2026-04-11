
namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed record ProductDetailsDto(
        int Id,
        string SKU,
        string Name,
        string? EAN,
        IReadOnlyCollection<ProductPriceDto> Prices,
        IReadOnlyCollection<ProductStockDto> Stocks
    );
}
