namespace SubiektNexoConnector.Core.Application.Products;

public sealed record MaterializedProductDetails(
    ProductDetailsDto Product,
    DateTime RefreshedAtUtc);
