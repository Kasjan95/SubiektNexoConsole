using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SubiektNexoConnector.Api.Controllers;
using SubiektNexoConnector.Api.Tests.TestDataBuilders;
using SubiektNexoConnector.Core.Application.Products;

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
        public void GetDetails_ReturnsOkWithProduct_WhenProductExists()
        {
            var repository = Substitute.For<IProductRepository>();
            var product = ProductDetailsDtoTestData.CreateProductDetailsDto();

            repository.GetDetails(product.SKU).Returns(product);

            var handler = new GetProductDetailsHandler(repository);
            var controller = new ProductsController();

            var actionResult = controller.GetDetails(product.SKU, handler);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var value = Assert.IsType<ProductDetailsDto>(okResult.Value);
            Assert.Equal(product, value);
        }

        [Fact]
        public void GetDetails_ReturnsNotFound_WhenProductDoesNotExist()
        {
            var repository = Substitute.For<IProductRepository>();
            const string productSku = "NON-EXISTENT-SKU";

            repository.GetDetails(productSku).Returns((ProductDetailsDto?)null);

            var handler = new GetProductDetailsHandler(repository);
            var controller = new ProductsController();

            var actionResult = controller.GetDetails(productSku, handler);

            Assert.IsType<NotFoundResult>(actionResult.Result);
        }
    }
}
