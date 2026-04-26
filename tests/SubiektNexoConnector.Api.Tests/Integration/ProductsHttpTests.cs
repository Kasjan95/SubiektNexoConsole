using FluentAssertions;
using NSubstitute;
using SubiektNexoConnector.Api.Tests.TestDataBuilders;
using SubiektNexoConnector.Core.Application.Products;
using System.Net;
using System.Net.Http.Json;

namespace SubiektNexoConnector.Api.Tests.Integration;
public class ProductsHttpTests : IClassFixture<TestApiFactory>
{
    private readonly HttpClient _client;
    private readonly TestApiFactory _factory;
    public ProductsHttpTests(TestApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateBusinessClient();
    }

    [Fact]
    public async Task GetProducts_Returns200AndJsonBody()
    {
        var products = new[]
        {
            new ProductBasicDto(1, "ABC-123", "Test product", "5901234567890")
        };

        _factory.Products.GetAll().Returns(products);

        var response = await _client.GetAsync("/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ProductBasicDto[]>();
        body.Should().BeEquivalentTo(products);
    }

    [Fact]
    public async Task GetProductDetails_Returns404_WhenWrongSku()
    {
        _factory.Products.GetDetails("non-existing").Returns((ProductDetailsDto?)null);

        var response = await _client.GetAsync("/products/non-existing");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetProductDetails_Returns200AndJsonBody() {
        var productDetails = ProductDetailsDtoTestData.CreateProductDetailsDto();
        _factory.Products.GetDetails(productDetails.SKU).Returns(productDetails);

        var response = await _client.GetAsync($"/products/{productDetails.SKU}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductDetailsDto>();
        body.Should().BeEquivalentTo(productDetails);
    }
}
