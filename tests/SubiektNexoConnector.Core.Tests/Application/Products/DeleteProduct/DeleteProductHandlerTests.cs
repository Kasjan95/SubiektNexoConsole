using NSubstitute;
using SubiektNexoConnector.Core.Application.Products;

namespace SubiektNexoConnector.Core.Tests.Application.Products
{
    public class DeleteProductHandlerTests
    {
        [Fact]
        public async Task HandleAsync_ReturnsDeleted_AndDeletesMaterialization_WhenRepositoryDeletesProduct()
        {
            var repository = Substitute.For<IProductRepository>();
            var synchronizer = Substitute.For<IProductReadModelSynchronizer>();
            var command = new DeleteProductCommand("PROD-001");
            repository.Delete(command).Returns(DeleteProductResult.Deleted);
            var handler = new DeleteProductHandler(repository, synchronizer);

            var result = await handler.HandleAsync(command);

            Assert.Equal(DeleteProductResult.Deleted, result);
            repository.Received(1).Delete(command);
            await synchronizer.Received(1).TryDeleteAsync(command.SKU, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task HandleAsync_ReturnsNotFound_AndDeletesStaleMaterialization_WhenRepositoryDoesNotFindProduct()
        {
            var repository = Substitute.For<IProductRepository>();
            var synchronizer = Substitute.For<IProductReadModelSynchronizer>();
            var command = new DeleteProductCommand("MISSING");
            repository.Delete(command).Returns(DeleteProductResult.NotFound);
            var handler = new DeleteProductHandler(repository, synchronizer);

            var result = await handler.HandleAsync(command);

            Assert.Equal(DeleteProductResult.NotFound, result);
            repository.Received(1).Delete(command);
            await synchronizer.Received(1).TryDeleteAsync(command.SKU, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task HandleAsync_ReturnsBlocked_AndKeepsMaterialization_WhenRepositoryBlocksDeletion()
        {
            var repository = Substitute.For<IProductRepository>();
            var synchronizer = Substitute.For<IProductReadModelSynchronizer>();
            var command = new DeleteProductCommand("PROD-001");
            repository.Delete(command).Returns(DeleteProductResult.Blocked);
            var handler = new DeleteProductHandler(repository, synchronizer);

            var result = await handler.HandleAsync(command);

            Assert.Equal(DeleteProductResult.Blocked, result);
            repository.Received(1).Delete(command);
            await synchronizer.DidNotReceiveWithAnyArgs().TryDeleteAsync(default!, default);
        }

        [Fact]
        public async Task HandleAsync_RethrowsException_FromRepository()
        {
            var repository = Substitute.For<IProductRepository>();
            var synchronizer = Substitute.For<IProductReadModelSynchronizer>();
            var command = new DeleteProductCommand("PROD-001");
            repository.Delete(command).Returns(_ => throw new ProductDeletionFailedException("Delete failed."));
            var handler = new DeleteProductHandler(repository, synchronizer);

            var exception = await Assert.ThrowsAsync<ProductDeletionFailedException>(() => handler.HandleAsync(command));

            Assert.Equal("Delete failed.", exception.Message);
            repository.Received(1).Delete(command);
            await synchronizer.DidNotReceiveWithAnyArgs().TryDeleteAsync(default!, default);
        }
    }
}
