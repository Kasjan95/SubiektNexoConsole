
namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed record ProductDetailsDto(
        int Id,
        string SKU,
        string Name,
        string? EAN,
        ProductTypeDto Type,
        bool IsActive,
        int? SupplierLeadTimeDays,
        IReadOnlyCollection<ProductSupplierDto> DefaultSuppliers,
        IReadOnlyCollection<ProductPriceDto> Prices,
        IReadOnlyCollection<ProductStockDto> Stocks
    );
}
