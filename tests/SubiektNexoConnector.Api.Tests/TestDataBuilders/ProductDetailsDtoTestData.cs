using SubiektNexoConnector.Core.Application.Products;

namespace SubiektNexoConnector.Api.Tests.TestDataBuilders;
public class ProductDetailsDtoTestData
{
    public static ProductDetailsDto CreateProductDetailsDto(
        string productSymbol = "PROD-001",
        string? ean = "1234567890123")
    {
        return new ProductDetailsDto(
            1,
            productSymbol,
            "Test product",
            ean,
            new ProductTypeDto("GOODS", "Goods"),
            true,
            7,
            new List<ProductSupplierDto>
            {
                new ProductSupplierDto(10, "Supplier 1", "1234567890", true, "SUP-001", "Supplier product")
            },
            new List<ProductPriceDto>
            {
                new ProductPriceDto("Retail", 10.00m, 12.30m)
            },
            new List<ProductStockDto>
            {
                new ProductStockDto("MAIN", 15m, 2m)
            }
        );
    }
}
