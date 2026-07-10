using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SubiektNexoConnector.Api.Controllers;
using SubiektNexoConnector.Api.Tests.TestDataBuilders;
using SubiektNexoConnector.Core.Application.Products;
using SubiektNexoConnector.Core.Application.Warehouses;

namespace SubiektNexoConnector.Api.Tests.Controllers;
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
        var product = ProductFromWarehouseDtoTestData.CreateProductFromWarehouseDto();

        repository.GetDetailsFromWarehouse(product.WarehouseSymbol, product.SKU).Returns(product);

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

        repository.GetDetailsFromWarehouse(warehouseSymbol, productSku).Returns((ProductFromWarehouseDto?)null);

        var handler = new GetProductFromWarehouseHandler(repository);
        var controller = new WarehousesController();

        var actionResult = controller.GetDetails(warehouseSymbol, productSku, handler);

        Assert.IsType<NotFoundResult>(actionResult.Result);
    }

}
