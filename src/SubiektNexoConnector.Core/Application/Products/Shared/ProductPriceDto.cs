namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed record ProductPriceDto(
        String PriceLevelName,
        decimal PriceNet,
        decimal PriceGross
        );
}
