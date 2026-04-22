using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SubiektNexoConnector.Api.Controllers;
using SubiektNexoConnector.Core.Application.Products;
using SubiektNexoConnector.Core.Application.Warehouses;

namespace SubiektNexoConnector.Api.Tests.Controllers
{
    public class WarehousesControllerTests
    {
        [Fact]
        public void GetAll_ReturnsOkWithWarehouses()
        {
            var repository = Substitute.For<IWarehouseRepository>();
            var warehouses = new List<WarehouseDto>
            {
                new WarehouseDto("MAIN", "Main warehouse"),
                new WarehouseDto("OUTLET", "Outlet warehouse")
            };

            repository.GetAll().Returns(warehouses);

            var handler = new GetWarehousesHandler(repository);
            var controller = new WarehousesController();

            var actionResult = controller.GetAll(handler);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var value = Assert.IsAssignableFrom<IReadOnlyCollection<WarehouseDto>>(okResult.Value);
            Assert.Equal(warehouses, value);
        }

        [Fact]
        public void GetAll_ReturnsOkWithEmptyCollection_WhenNoWarehousesExist()
        {
            var repository = Substitute.For<IWarehouseRepository>();
            var warehouses = new List<WarehouseDto>();

            repository.GetAll().Returns(warehouses);

            var handler = new GetWarehousesHandler(repository);
            var controller = new WarehousesController();

            var actionResult = controller.GetAll(handler);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var value = Assert.IsAssignableFrom<IReadOnlyCollection<WarehouseDto>>(okResult.Value);
            Assert.Empty(value);
        }

        [Fact]
        public void GetDetails_ReturnsOkWithProduct_WhenProductExistsInWarehouse()
        {
            var repository = Substitute.For<IProductRepository>();
            var product = CreateProductFromWarehouseDto();

            repository.GetDetailsFromWarehouse(product.SKU, product.WarehouseSymbol).Returns(product);

            var handler = new GetProductFromWarehouseHandler(repository);
            var controller = new WarehousesController();

            var actionResult = controller.GetDetails(product.WarehouseSymbol, product.SKU, handler);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var value = Assert.IsType<ProductFromWarehouseDto>(okResult.Value);
            Assert.Equal(product, value);
        }

        [Fact]
        public void GetDetails_ReturnsNotFound_WhenProductDoesNotExistInWarehouse()
        {
            var repository = Substitute.For<IProductRepository>();
            const string warehouseSymbol = "MAIN";
            const string productSku = "NON-EXISTENT-SKU";

            repository.GetDetailsFromWarehouse(productSku, warehouseSymbol).Returns((ProductFromWarehouseDto?)null);

            var handler = new GetProductFromWarehouseHandler(repository);
            var controller = new WarehousesController();

            var actionResult = controller.GetDetails(warehouseSymbol, productSku, handler);

            Assert.IsType<NotFoundResult>(actionResult.Result);
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
}
