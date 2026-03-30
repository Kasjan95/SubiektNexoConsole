namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed record ProductBasicDto(
        int Id,
        string SKU,
        string Name,
        string? EAN
    );
}
