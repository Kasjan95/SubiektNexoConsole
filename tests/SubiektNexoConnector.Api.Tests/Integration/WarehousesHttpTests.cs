

using FluentAssertions;
using NSubstitute;
using SubiektNexoConnector.Api.Tests.TestDataBuilders;
using SubiektNexoConnector.Core.Application.Products;
using SubiektNexoConnector.Core.Application.Warehouses;
using System.Net;
using System.Net.Http.Json;

namespace SubiektNexoConnector.Api.Tests.Integration;
public class WarehousesHttpTests : IClassFixture<TestApiFactory>
{
    private readonly HttpClient _client;
    private readonly TestApiFactory _factory;
    public WarehousesHttpTests(TestApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateBusinessClient();
    }

    [Fact]
    public async Task GetWarehouses_Return200AndJsonBody()
    {
        var warehouses = new List<WarehouseDto>
        {
            new WarehouseDto("MAIN", "Warehouse 1"),
            new WarehouseDto("SECONDARY", "Warehouse 2")
        };
        _factory.Warehouses.GetAll().Returns(warehouses);

        var response = await _client.GetAsync("/warehouses");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<WarehouseDto>>();
        body.Should().BeEquivalentTo(warehouses);
    }

    [Fact]
    public async Task GetProductFromWarehouse_Returns200AndJsonBody()
    {
        var productFromWarehouse = ProductFromWarehouseDtoTestData.CreateProductFromWarehouseDto();
        _factory.Products.GetDetailsFromWarehouse(productFromWarehouse.WarehouseSymbol, productFromWarehouse.SKU).Returns(productFromWarehouse);

        var response = await _client.GetAsync($"/Warehouses/{productFromWarehouse.WarehouseSymbol}/products/{productFromWarehouse.SKU}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductFromWarehouseDto>();
        body.Should().BeEquivalentTo(productFromWarehouse);
    }

    [Fact]
    public async Task GetProductFromWarehouse_Returns404_WhenWrongProductOrWarehouseSku()
    {
        _factory.Products.GetDetailsFromWarehouse("MISSING", "PRODUCT").Returns((ProductFromWarehouseDto?)null);
        _factory.Products.GetDetailsFromWarehouse("WAREHOUSE", "MISSING").Returns((ProductFromWarehouseDto?)null);

        var missing_warehouse_response = await _client.GetAsync($"/Warehouses/MISSING/products/PRODUCT");
        var missing_product_response = await _client.GetAsync($"/Warehouses/WAREHOUSE/products/MISSING");

        missing_warehouse_response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        missing_product_response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
