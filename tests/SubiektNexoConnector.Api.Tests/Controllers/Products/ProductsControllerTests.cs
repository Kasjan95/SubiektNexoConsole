using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SubiektNexoConnector.Api.Controllers;
using SubiektNexoConnector.Api.Tests.TestDataBuilders;
using SubiektNexoConnector.Core.Application.Products;
using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Api.Tests.Controllers
{
    public class ProductsControllerTests
    {
        [Fact]
        public void GetAll_ReturnsOkWithProducts()
        {
            var repository = Substitute.For<IProductRepository>();
            var products = new List<ProductBasicDto>
            {
                new ProductBasicDto(1, "PROD-001", "Product 1", "1234567890123"),
                new ProductBasicDto(2, "PROD-002", "Product 2", null)
            };

            repository.GetAll(Arg.Any<GetProductsQuery>()).Returns(products);

            var handler = new GetProductsHandler(repository);
            var controller = new ProductsController();

            var actionResult = controller.GetAll(handler);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var value = Assert.IsAssignableFrom<IReadOnlyCollection<ProductBasicDto>>(okResult.Value);
            Assert.Equal(products, value);
        }

        [Fact]
        public void GetAll_ReturnsOkWithEmptyCollection_WhenNoProductsExist()
        {
            var repository = Substitute.For<IProductRepository>();
            var products = new List<ProductBasicDto>();

            repository.GetAll(Arg.Any<GetProductsQuery>()).Returns(products);

            var handler = new GetProductsHandler(repository);
            var controller = new ProductsController();

            var actionResult = controller.GetAll(handler);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var value = Assert.IsAssignableFrom<IReadOnlyCollection<ProductBasicDto>>(okResult.Value);
            Assert.Empty(value);
        }

        [Fact]
        public void GetAll_ReturnsValidationProblem_WhenPageIsOutsideContract()
        {
            var repository = Substitute.For<IProductRepository>();
            var handler = new GetProductsHandler(repository);
            var controller = new ProductsController();

            var actionResult = controller.GetAll(handler, page: 0);

            var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
            var problem = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
            Assert.Contains("page", problem.Errors.Keys);
            repository.DidNotReceive().GetAll(Arg.Any<GetProductsQuery>());
        }

        [Fact]
        public async Task GetDetails_ReturnsOkWithProduct_WhenProductExists()
        {
            var reader = Substitute.For<IProductReadModelReader>();
            var refresher = Substitute.For<IProductMaterializationRefresher>();
            var freshnessPolicy = Substitute.For<IProductFreshnessPolicy>();
            var product = ProductDetailsDtoTestData.CreateProductDetailsDto();
            var refreshedAtUtc = DateTime.UtcNow;

            reader.GetDetailsAsync(product.SKU, Arg.Any<CancellationToken>())
                .Returns(new MaterializedProductDetails(product, refreshedAtUtc));
            freshnessPolicy.GetMetadataAsync(
                    product,
                    refreshedAtUtc,
                    Arg.Any<CancellationToken>())
                .Returns(new MaterializationMetadata(MaterializationStatus.Fresh, refreshedAtUtc));

            var handler = new GetProductDetailsHandler(reader, refresher, freshnessPolicy);
            var controller = CreateController();

            var actionResult = await controller.GetDetails(product.SKU, handler, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var value = Assert.IsType<ProductDetailsDto>(okResult.Value);
            Assert.Equal(product, value);
        }

        [Fact]
        public async Task GetDetails_ReturnsNotFound_WhenProductDoesNotExist()
        {
            var reader = Substitute.For<IProductReadModelReader>();
            var refresher = Substitute.For<IProductMaterializationRefresher>();
            var freshnessPolicy = Substitute.For<IProductFreshnessPolicy>();
            const string productSku = "NON-EXISTENT-SKU";

            reader.GetDetailsAsync(productSku, Arg.Any<CancellationToken>())
                .Returns((MaterializedProductDetails?)null);

            var handler = new GetProductDetailsHandler(reader, refresher, freshnessPolicy);
            var controller = CreateController();

            var actionResult = await controller.GetDetails(productSku, handler, CancellationToken.None);

            Assert.IsType<NotFoundResult>(actionResult.Result);
        }

        private static ProductsController CreateController() => new()
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }
}
