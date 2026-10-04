namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class ProductPriceEntity
{
    public int ProductId { get; set; }
    public required string PriceLevelName { get; set; }
    public decimal PriceNet { get; set; }
    public decimal PriceGross { get; set; }
    public DateTime RefreshedAtUtc { get; set; }
}