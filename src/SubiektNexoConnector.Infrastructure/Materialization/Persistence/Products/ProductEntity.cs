namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class ProductEntity
{
    public int Id { get; set; }
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public string? EAN { get; set; }
    public required string TypeSymbol { get; set; }
    public required string TypeName { get; set; }
    public bool IsActive { get; set; }
    public int? FlagId { get; set; }
    public string? FlagComment { get; set; }
    public int? SupplierLeadTimeDays { get; set; }
    public DateTime RefreshedAtUtc { get; set; }
    public ProductBasicFieldsEntity? BasicFields { get; set; }
    public ICollection<ProductAdvancedFieldEntity> AdvancedFields { get; set; } = [];
    public ICollection<ProductPriceEntity> Prices { get; set; } = [];
    public ICollection<ProductStockEntity> Stocks { get; set; } = [];
    public ICollection<ProductSupplierEntity> Suppliers { get; set; } = [];
}
