using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SubiektNexoConnector.Api.Tests.TestDataBuilders;
using SubiektNexoConnector.Core.Application.Products;
using System.Net;
using Microsoft.AspNetCore.Http;
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
    public async Task GetProductDetails_Returns200AndJsonBody()
    {
        var productDetails = ProductDetailsDtoTestData.CreateProductDetailsDto();
        _factory.Products.GetDetails(productDetails.SKU).Returns(productDetails);

        var response = await _client.GetAsync($"/products/{productDetails.SKU}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductDetailsDto>();
        body.Should().BeEquivalentTo(productDetails);
    }

    [Fact]
    public async Task CreateProduct_Returns201LocationAndJsonBody()
    {
        var request = new CreateProductRequestDto(
            "Test product",
            "ABC-123",
            "5901234567890");

        _factory.Products
            .Create(Arg.Is<CreateProductCommand>(command =>
                command.Name == request.Name &&
                command.SKU == request.SKU &&
                command.EAN == request.EAN))
            .Returns("ABC-123");

        var response = await _client.PostAsJsonAsync("/products", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull(); 
        response.Headers.Location!.AbsolutePath.Should().Be("/Products/ABC-123");

        var body = await response.Content.ReadFromJsonAsync<CreateProductResponseDto>();
        body.Should().BeEquivalentTo(new CreateProductResponseDto("ABC-123"));
    }

    [Fact]
    public async Task CreateProduct_Returns400ProblemDetails_WhenCreationFails()
    {
        var request = new CreateProductRequestDto(
            "Test product",
            "ABC-123",
            "5901234567890");

        _factory.Products
            .Create(Arg.Any<CreateProductCommand>())
            .Returns(_ => throw new InvalidOperationException("Product already exists."));

        var response = await _client.PostAsJsonAsync("/products", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        body.Should().NotBeNull();
        body!.Status.Should().Be(StatusCodes.Status400BadRequest);
        body.Title.Should().Be("Bad Request");
        body.Detail.Should().Be("Product already exists.");
        body.Instance.Should().Be("/products");
    }

    [Fact]
    public async Task CreateProduct_Returns400_WhenRequestIsInvalid()
    {
        var response = await _client.PostAsJsonAsync(
            "/products",
            new
            {
                SKU = "ABC-123",
                EAN = "5901234567890"
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteProduct_Returns204_WhenProductWasDeleted()
    {
        _factory.Products
            .Delete(Arg.Is<DeleteProductCommand>(command => command.SKU == "ABC-123"))
            .Returns(DeleteProductResult.Deleted);

        var response = await _client.DeleteAsync("/products/ABC-123");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteProduct_Returns404_WhenProductWasNotFound()
    {
        _factory.Products
            .Delete(Arg.Is<DeleteProductCommand>(command => command.SKU == "missing"))
            .Returns(DeleteProductResult.NotFound);

        var response = await _client.DeleteAsync("/products/missing");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteProduct_Returns409ProblemDetails_WhenProductIsBlocked()
    {
        _factory.Products
            .Delete(Arg.Is<DeleteProductCommand>(command => command.SKU == "ABC-123"))
            .Returns(DeleteProductResult.Blocked);

        var response = await _client.DeleteAsync("/products/ABC-123");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var body = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        body.Should().NotBeNull();
        body!.Status.Should().Be(StatusCodes.Status409Conflict);
        body.Title.Should().Be("Product cannot be deleted");
        body.Detail.Should().Be("Product was used in documents and cannot be removed.");
    }
}
