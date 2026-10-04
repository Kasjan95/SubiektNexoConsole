namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class ProductSupplierEntity
{
    public int ProductId { get; set; }
    public int SupplierId { get; set; }
    public required string Name { get; set; }
    public string? Nip { get; set; }
    public bool IsPrimary { get; set; }
    public string? SupplierProductSymbol { get; set; }
    public string? SupplierProductName { get; set; }
    public DateTime RefreshedAtUtc { get; set; }
}