using NSubstitute;
using SubiektNexoConnector.Core.Application.Products;

namespace SubiektNexoConnector.Core.Tests.Application.Products
{
    public class GetProductsHandlerTests
    {
        [Fact]
        public void Handle_ReturnsProductsFromRepository()
        {
            var repository = Substitute.For<IProductRepository>();
            var expectedProducts = new List<ProductBasicDto>
            {
                new ProductBasicDto(1, "Prod.1.SKU", "Product 1 Name" ,"1234567890"),
                new ProductBasicDto(2, "Prod.2.SKU", "Product 2 Name" , null)
            };
            repository.GetAll().Returns(expectedProducts);
            var handler = new GetProductsHandler(repository);
            var query = new GetProductsQuery();

            var result = handler.Handle(query);

            Assert.Equal(expectedProducts, result);
            Assert.Contains(result, p => p.SKU == "Prod.2.SKU" && p.EAN == null);
            repository.Received(1).GetAll();
        }

        [Fact]
        public void Handle_ReturnsEmptyCollection_WhenRepositoryIsEmpty()
        {
            var repository = Substitute.For<IProductRepository>();
            repository.GetAll().Returns(new List<ProductBasicDto>());
            var handler = new GetProductsHandler(repository);
            var query = new GetProductsQuery();

            var result = handler.Handle(query);

            Assert.Empty(result);
            repository.Received(1).GetAll();
        }
    }
}