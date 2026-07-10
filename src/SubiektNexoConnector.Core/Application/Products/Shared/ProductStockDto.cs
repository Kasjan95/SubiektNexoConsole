
namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed record ProductStockDto(
        String WarehouseSymbol,
        decimal AvailableStock,
        decimal ReservedStock
        );
}
