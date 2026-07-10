using NSubstitute;
using SubiektNexoConnector.Core.Application.Products;

namespace SubiektNexoConnector.Core.Tests.Application.Products
{
    public class CreateProductHandlerTests
    {
        [Fact]
        public void Handle_ReturnsSkuFromRepository()
        {
            var repository = Substitute.For<IProductRepository>();
            var command = new CreateProductCommand("Test product", "PROD-001", "5901234567890");
            repository.Create(command).Returns("PROD-001");
            var handler = new CreateProductHandler(repository);

            var result = handler.Handle(command);

            Assert.Equal("PROD-001", result);
            repository.Received(1).Create(command);
        }

        [Fact]
        public void Handle_RethrowsException_FromRepository()
        {
            var repository = Substitute.For<IProductRepository>();
            var command = new CreateProductCommand("Test product", "PROD-001", null);
            repository.Create(command).Returns(_ => throw new InvalidOperationException("Product already exists."));
            var handler = new CreateProductHandler(repository);

            var exception = Assert.Throws<InvalidOperationException>(() => handler.Handle(command));

            Assert.Equal("Product already exists.", exception.Message);
            repository.Received(1).Create(command);
        }
    }
}
