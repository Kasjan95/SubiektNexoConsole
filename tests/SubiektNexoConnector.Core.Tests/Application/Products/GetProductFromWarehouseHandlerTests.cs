

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
            repository.GetDetailsFromWarehouse(expectedResult.SKU, expectedResult.WarehouseSymbol).Returns(expectedResult);
            var handler = new GetProductFromWarehouseHandler(repository);
            var query = new GetProductFromWarehouseQuery(expectedResult.SKU, expectedResult.WarehouseSymbol);

            var result = handler.Handle(query);

            Assert.Equal(expectedResult, result);
            repository.Received(1).GetDetailsFromWarehouse(expectedResult.SKU, expectedResult.WarehouseSymbol);
        }
        [Fact]
        public void Handle_ReturnsNull_WhenProductOrWarehouseDoesNotExist()
        {
            var repository = Substitute.For<IProductRepository>();
            repository.GetDetailsFromWarehouse("NON-EXISTENT-SKU", "MAIN").Returns((ProductFromWarehouseDto?)null);
            repository.GetDetailsFromWarehouse("PROD-001", "NON-EXISTENT-WAREHOUSE").Returns((ProductFromWarehouseDto?)null);
            var handler = new GetProductFromWarehouseHandler(repository);
            var query1 = new GetProductFromWarehouseQuery("NON-EXISTENT-SKU", "MAIN");
            var query2 = new GetProductFromWarehouseQuery("PROD-001", "NON-EXISTENT-WAREHOUSE");

            var result1 = handler.Handle(query1);
            var result2 = handler.Handle(query2);

            Assert.Null(result1);
            Assert.Null(result2);
            repository.Received(1).GetDetailsFromWarehouse("NON-EXISTENT-SKU", "MAIN");
            repository.Received(1).GetDetailsFromWarehouse("PROD-001", "NON-EXISTENT-WAREHOUSE");
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
