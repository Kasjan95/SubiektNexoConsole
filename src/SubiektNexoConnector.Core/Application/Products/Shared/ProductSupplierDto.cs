namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed record ProductSupplierDto(
        int Id,
        string Name,
        string? Nip,
        bool IsPrimary,
        string? SupplierProductSymbol,
        string? SupplierProductName
    );
}
