namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class ProductStockEntity
{
    public int ProductId { get; set; }
    public required string WarehouseSymbol { get; set; }
    public decimal AvailableStock { get; set; }
    public decimal ReservedStock { get; set; }
    public DateTime RefreshedAtUtc { get; set; }
}