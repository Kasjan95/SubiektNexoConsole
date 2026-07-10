namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed record CreateProductCommand(
        string Name,
        string? SKU,
        string? EAN
        );
}
