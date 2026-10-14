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
        public async Task GetAll_ReturnsOkWithProducts()
        {
            var reader = Substitute.For<IProductReadModelReader>();
            var checkpointStore = Substitute.For<IMaterializationCheckpointStore>();
            var freshnessEvaluator = Substitute.For<IMaterializationFreshnessEvaluator>();
            var products = new List<ProductBasicDto>
            {
                new ProductBasicDto(1, "PROD-001", "Product 1", "1234567890123"),
                new ProductBasicDto(2, "PROD-002", "Product 2", null)
            };
            ConfigureCompletedDiscovery(checkpointStore, freshnessEvaluator);
            reader.GetAllAsync(Arg.Any<GetProductsQuery>(), Arg.Any<CancellationToken>()).Returns(products);
            var handler = new GetProductsHandler(reader, checkpointStore, freshnessEvaluator);
            var controller = CreateController();

            var actionResult = await controller.GetAll(handler, cancellationToken: CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var value = Assert.IsAssignableFrom<IReadOnlyCollection<ProductBasicDto>>(okResult.Value);
            Assert.Equal(products, value);
        }

        [Fact]
        public async Task GetAll_ReturnsOkWithEmptyCollection_WhenNoProductsExist()
        {
            var reader = Substitute.For<IProductReadModelReader>();
            var checkpointStore = Substitute.For<IMaterializationCheckpointStore>();
            var freshnessEvaluator = Substitute.For<IMaterializationFreshnessEvaluator>();
            var products = new List<ProductBasicDto>();
            ConfigureCompletedDiscovery(checkpointStore, freshnessEvaluator);
            reader.GetAllAsync(Arg.Any<GetProductsQuery>(), Arg.Any<CancellationToken>()).Returns(products);
            var handler = new GetProductsHandler(reader, checkpointStore, freshnessEvaluator);
            var controller = CreateController();

            var actionResult = await controller.GetAll(handler, cancellationToken: CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var value = Assert.IsAssignableFrom<IReadOnlyCollection<ProductBasicDto>>(okResult.Value);
            Assert.Empty(value);
        }

        [Fact]
        public async Task GetAll_ReturnsValidationProblem_WhenPageIsOutsideContract()
        {
            var reader = Substitute.For<IProductReadModelReader>();
            var checkpointStore = Substitute.For<IMaterializationCheckpointStore>();
            var freshnessEvaluator = Substitute.For<IMaterializationFreshnessEvaluator>();
            var handler = new GetProductsHandler(reader, checkpointStore, freshnessEvaluator);
            var controller = CreateController();

            var actionResult = await controller.GetAll(handler, page: 0, cancellationToken: CancellationToken.None);

            var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
            var problem = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
            Assert.Contains("page", problem.Errors.Keys);
            await checkpointStore.DidNotReceive().GetOrCreateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
            await reader.DidNotReceive().GetAllAsync(Arg.Any<GetProductsQuery>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task GetAll_Returns503_WhenInitialDiscoveryHasNotCompleted()
        {
            var reader = Substitute.For<IProductReadModelReader>();
            var checkpointStore = Substitute.For<IMaterializationCheckpointStore>();
            var freshnessEvaluator = Substitute.For<IMaterializationFreshnessEvaluator>();
            checkpointStore.GetOrCreateAsync(MaterializationPolicyDefinitions.ProductsNew.Id, Arg.Any<CancellationToken>()).Returns(CreateCheckpoint(null));
            var handler = new GetProductsHandler(reader, checkpointStore, freshnessEvaluator);
            var controller = CreateController();

            var actionResult = await controller.GetAll(handler, cancellationToken: CancellationToken.None);

            var statusResult = Assert.IsType<StatusCodeResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusResult.StatusCode);
            await reader.DidNotReceive().GetAllAsync(Arg.Any<GetProductsQuery>(), Arg.Any<CancellationToken>());
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

        private static void ConfigureCompletedDiscovery(IMaterializationCheckpointStore checkpointStore, IMaterializationFreshnessEvaluator freshnessEvaluator)
        {
            var definition = MaterializationPolicyDefinitions.ProductsNew;
            var completedAtUtc = DateTime.UtcNow;
            checkpointStore.GetOrCreateAsync(definition.Id, Arg.Any<CancellationToken>()).Returns(CreateCheckpoint(completedAtUtc));
            freshnessEvaluator.EvaluateAsync(definition, completedAtUtc, Arg.Any<CancellationToken>()).Returns(new MaterializationMetadata(MaterializationStatus.Fresh, completedAtUtc));
        }

        private static MaterializationCheckpoint CreateCheckpoint(DateTime? completedAtUtc) => new(MaterializationPolicyDefinitions.ProductsNew.Id, null, null, null, completedAtUtc, null, null);
    }
}
