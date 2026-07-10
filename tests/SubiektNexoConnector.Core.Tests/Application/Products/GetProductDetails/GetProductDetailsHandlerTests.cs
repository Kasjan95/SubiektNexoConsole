using SubiektNexoConnector.Core.Application.Products;
using NSubstitute;

namespace SubiektNexoConnector.Core.Tests.Application.Products
{
    public class GetProductDetailsHandlerTests
    {
        [Fact]
        public void Handle_ReturnsProductDetailsFromRepository()
        {
            var repository = Substitute.For<IProductRepository>();
            var expectedProduct = GetProductDetailsHandlerTests.CreateProductDetailsDto("PROD-001");
            repository.GetDetails(expectedProduct.SKU).Returns(expectedProduct);
            var handler = new GetProductDetailsHandler(repository);
            var query = new GetProductDetailsQuery(expectedProduct.SKU);

            var result = handler.Handle(query);

            Assert.Equal(expectedProduct, result);
            repository.Received(1).GetDetails(expectedProduct.SKU);
        }
        [Fact]
        public void Handle_ReturnsNull_WhenProductDoesNotExist()
        {
            var repository = Substitute.For<IProductRepository>();
            repository.GetDetails("NON-EXISTENT-SKU").Returns((ProductDetailsDto?)null);
            var handler = new GetProductDetailsHandler(repository);
            var query = new GetProductDetailsQuery("NON-EXISTENT-SKU");

            var result = handler.Handle(query);

            Assert.Null(result);
            repository.Received(1).GetDetails("NON-EXISTENT-SKU");
        }   

        private static ProductDetailsDto CreateProductDetailsDto(
            string productSymbol,
            string? ean = "1234567890",
            int? SupplierLeadTimeDays = 7)
        {
            return new ProductDetailsDto(
                1,
                productSymbol,
                "Test product",
                ean,
                new ProductTypeDto("product", "product"),
                true,
                SupplierLeadTimeDays,
                new List<ProductSupplierDto>
                {
            new ProductSupplierDto(10, "Supplier 1", "1234567890", true, "SUP-001", "Supplier Product")
                },
                new List<ProductPriceDto>
                {
            new ProductPriceDto("Detail", 10.00m, 12.30m)
                },
                new List<ProductStockDto>
                {
            new ProductStockDto("WAR-A", 15m, 2m)
                }
            );
        }
    }
}
