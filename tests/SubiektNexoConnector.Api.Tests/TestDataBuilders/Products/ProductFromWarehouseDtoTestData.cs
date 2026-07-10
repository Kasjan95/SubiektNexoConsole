using SubiektNexoConnector.Core.Application.Products;

namespace SubiektNexoConnector.Api.Tests.TestDataBuilders;
public class ProductFromWarehouseDtoTestData
{

    public static ProductFromWarehouseDto CreateProductFromWarehouseDto(
        string productSymbol = "PROD-001",
        string warehouseSymbol = "MAIN",
        string? ean = "1234567890123")
    {
        return new ProductFromWarehouseDto(
            1,
            productSymbol,
            "Test product",
            ean,
            warehouseSymbol,
            25m,
            5m,
            CreateStockMovementDto("Receipt"),
            CreateStockMovementDto("Issue"),
            CreateStockMovementDto("Return")
        );
    }

    private static StockMovementDto CreateStockMovementDto(
        string operationType,
        int documentCount = 2,
        decimal totalQuantity = 20m)
    {
        return new StockMovementDto(
            documentCount,
            totalQuantity,
            new List<StockOperationDto>
            {
                new StockOperationDto("DOC-001", new DateTime(2026, 4, 22), 10m, operationType),
                new StockOperationDto("DOC-002", new DateTime(2026, 4, 23), 10m, operationType)
            }
        );
    }
}
