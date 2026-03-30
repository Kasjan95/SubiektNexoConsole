namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed record ProductDetailsDto(
        int Id,
        string SKU,
        string Name,
        string? EAN,
        string WarehouseSymbol,
        decimal Available,
        decimal Reserved,
        StockMovementDto Receipts,
        StockMovementDto Issues,
        StockMovementDto Returns
    );
}
