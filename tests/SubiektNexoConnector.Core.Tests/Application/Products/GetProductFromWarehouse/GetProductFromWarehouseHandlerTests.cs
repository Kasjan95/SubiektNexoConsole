

using NSubstitute;
using SubiektNexoConnector.Core.Application.Products;

namespace SubiektNexoConnector.Core.Tests.Application.Products
{
    public class GetProductFromWarehouseHandlerTests
    {
        [Fact]
        public void Handle_ReturnsProductFromWarehouse_WhenProductExistsInWarehouse()
        {
            var repository = Substitute.For<IProductRepository>();
            ProductFromWarehouseDto expectedResult = GetProductFromWarehouseHandlerTests.CreateProductFromWarehouseDto();
            repository.GetDetailsFromWarehouse(expectedResult.WarehouseSymbol, expectedResult.SKU).Returns(expectedResult);
            var handler = new GetProductFromWarehouseHandler(repository);
            var query = new GetProductFromWarehouseQuery(expectedResult.WarehouseSymbol, expectedResult.SKU);

            var result = handler.Handle(query);

            Assert.Equal(expectedResult, result);
            repository.Received(1).GetDetailsFromWarehouse(expectedResult.WarehouseSymbol, expectedResult.SKU);
        }
        [Fact]
        public void Handle_ReturnsNull_WhenProductOrWarehouseDoesNotExist()
        {
            var repository = Substitute.For<IProductRepository>();
            repository.GetDetailsFromWarehouse("MAIN", "NON-EXISTENT-SKU").Returns((ProductFromWarehouseDto?)null);
            repository.GetDetailsFromWarehouse("NON-EXISTENT-WAREHOUSE", "PROD-001").Returns((ProductFromWarehouseDto?)null);
            var handler = new GetProductFromWarehouseHandler(repository);
            var query1 = new GetProductFromWarehouseQuery("MAIN", "NON-EXISTENT-SKU");
            var query2 = new GetProductFromWarehouseQuery("NON-EXISTENT-WAREHOUSE", "PROD-001");

            var result1 = handler.Handle(query1);
            var result2 = handler.Handle(query2);

            Assert.Null(result1);
            Assert.Null(result2);
            repository.Received(1).GetDetailsFromWarehouse("MAIN", "NON-EXISTENT-SKU");
            repository.Received(1).GetDetailsFromWarehouse("NON-EXISTENT-WAREHOUSE", "PROD-001");
        }
        private static ProductFromWarehouseDto CreateProductFromWarehouseDto(
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
                GetProductFromWarehouseHandlerTests.CreateStockMovementDto("Receipt"),
                GetProductFromWarehouseHandlerTests.CreateStockMovementDto("Issue"),
                GetProductFromWarehouseHandlerTests.CreateStockMovementDto("Return")
            );
        }
        private static StockOperationDto CreateStockOperationDto(
            string type,
            string documentNumber = "GR-2026-0001",
            DateTime date = default,
            decimal quantity = 10m)
        {
            return new StockOperationDto(documentNumber, date == default ? new DateTime(2026, 4, 21) : date, quantity, type);
        }
        private static StockMovementDto CreateStockMovementDto(
            string movementType,
            int documentCount = 2,
            decimal totalQuantity = 20m)
        {
            List<StockOperationDto> operations = new List<StockOperationDto>
            {
                CreateStockOperationDto(movementType, "GR-2026-0001"),
                CreateStockOperationDto(movementType, "GR-2026-0002")
            };
            StockMovementDto movement = new StockMovementDto(documentCount, totalQuantity, operations);

            return movement;
        }
    }
}
