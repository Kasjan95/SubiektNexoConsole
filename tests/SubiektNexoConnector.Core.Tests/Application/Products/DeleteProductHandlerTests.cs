using NSubstitute;
using SubiektNexoConnector.Core.Application.Products;

namespace SubiektNexoConnector.Core.Tests.Application.Products
{
    public class DeleteProductHandlerTests
    {
        [Fact]
        public void Handle_ReturnsDeleted_WhenRepositoryDeletesProduct()
        {
            var repository = Substitute.For<IProductRepository>();
            var command = new DeleteProductCommand("PROD-001");
            repository.Delete(command).Returns(DeleteProductResult.Deleted);
            var handler = new DeleteProductHandler(repository);

            var result = handler.Handle(command);

            Assert.Equal(DeleteProductResult.Deleted, result);
            repository.Received(1).Delete(command);
        }

        [Fact]
        public void Handle_ReturnsNotFound_WhenRepositoryDoesNotFindProduct()
        {
            var repository = Substitute.For<IProductRepository>();
            var command = new DeleteProductCommand("MISSING");
            repository.Delete(command).Returns(DeleteProductResult.NotFound);
            var handler = new DeleteProductHandler(repository);

            var result = handler.Handle(command);

            Assert.Equal(DeleteProductResult.NotFound, result);
            repository.Received(1).Delete(command);
        }

        [Fact]
        public void Handle_ReturnsBlocked_WhenRepositoryBlocksDeletion()
        {
            var repository = Substitute.For<IProductRepository>();
            var command = new DeleteProductCommand("PROD-001");
            repository.Delete(command).Returns(DeleteProductResult.Blocked);
            var handler = new DeleteProductHandler(repository);

            var result = handler.Handle(command);

            Assert.Equal(DeleteProductResult.Blocked, result);
            repository.Received(1).Delete(command);
        }

        [Fact]
        public void Handle_RethrowsException_FromRepository()
        {
            var repository = Substitute.For<IProductRepository>();
            var command = new DeleteProductCommand("PROD-001");
            repository.Delete(command).Returns(_ => throw new ProductDeletionFailedException("Delete failed."));
            var handler = new DeleteProductHandler(repository);

            var exception = Assert.Throws<ProductDeletionFailedException>(() => handler.Handle(command));

            Assert.Equal("Delete failed.", exception.Message);
            repository.Received(1).Delete(command);
        }
    }
}
