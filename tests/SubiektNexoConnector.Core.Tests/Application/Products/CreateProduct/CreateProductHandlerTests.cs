using NSubstitute;
using SubiektNexoConnector.Core.Application.Products;

namespace SubiektNexoConnector.Core.Tests.Application.Products
{
    public class CreateProductHandlerTests
    {
        [Fact]
        public async Task HandleAsync_ReturnsCreatedProductAndUpdatesReadModel()
        {
            var repository = Substitute.For<IProductRepository>();
            var synchronizer = Substitute.For<IProductReadModelSynchronizer>();
            var command = new CreateProductCommand("Test product", "PROD-001", "5901234567890", []);
            var product = new ProductDetailsDto(1, "PROD-001", "Test product", "5901234567890", new ProductTypeDto("GOODS", "Goods"), true, null, null, [], [], [], [], []);
            repository.Create(command).Returns(product);
            synchronizer.TryUpsertAsync(product, Arg.Any<CancellationToken>()).Returns(true);
            var handler = new CreateProductHandler(repository, synchronizer);

            var result = await handler.HandleAsync(command);

            Assert.Equal(product, result);
            repository.Received(1).Create(command);
            await synchronizer.Received(1).TryUpsertAsync(product, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task HandleAsync_RethrowsException_FromRepository()
        {
            var repository = Substitute.For<IProductRepository>();
            var synchronizer = Substitute.For<IProductReadModelSynchronizer>();
            var command = new CreateProductCommand("Test product", "PROD-001", null, []);
            repository.Create(command).Returns(_ => throw new InvalidOperationException("Product already exists."));
            var handler = new CreateProductHandler(repository, synchronizer);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(command));

            Assert.Equal("Product already exists.", exception.Message);
            repository.Received(1).Create(command);
            await synchronizer.DidNotReceive().TryUpsertAsync(Arg.Any<ProductDetailsDto>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task HandleAsync_ReturnsCreatedProduct_WhenReadModelUpdateFails()
        {
            var repository = Substitute.For<IProductRepository>();
            var synchronizer = Substitute.For<IProductReadModelSynchronizer>();
            var command = new CreateProductCommand("Test product", "PROD-001", null, []);
            var product = new ProductDetailsDto(1, "PROD-001", "Test product", null, new ProductTypeDto("GOODS", "Goods"), true, null, null, [], [], [], [], []);
            repository.Create(command).Returns(product);
            synchronizer.TryUpsertAsync(product, Arg.Any<CancellationToken>()).Returns(false);
            var handler = new CreateProductHandler(repository, synchronizer);

            var result = await handler.HandleAsync(command);

            Assert.Equal(product, result);
        }
    }
}
