using NSubstitute;
using SubiektNexoConnector.Core.Application.Products;

namespace SubiektNexoConnector.Core.Tests.Application.Products;

public sealed class GetProductDetailsHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsFreshMaterializedProduct_WithoutRefreshing()
    {
        var reader = Substitute.For<IProductReadModelReader>();
        var refresher = Substitute.For<IProductMaterializationRefresher>();
        var freshnessPolicy = Substitute.For<IProductFreshnessPolicy>();
        var expectedProduct = CreateProductDetailsDto("PROD-001");
        var refreshedAtUtc = new DateTime(2026, 10, 8, 8, 0, 0, DateTimeKind.Utc);
        reader.GetDetailsAsync(expectedProduct.SKU, Arg.Any<CancellationToken>())
            .Returns(new MaterializedProductDetails(expectedProduct, refreshedAtUtc));
        freshnessPolicy.GetRefreshDueAtUtcAsync(
                expectedProduct,
                refreshedAtUtc,
                Arg.Any<CancellationToken>())
            .Returns(DateTime.MaxValue);
        var handler = new GetProductDetailsHandler(reader, refresher, freshnessPolicy);

        var result = await handler.HandleAsync(new GetProductDetailsQuery(expectedProduct.SKU));

        Assert.Equal(expectedProduct, result);
        await reader.Received(1).GetDetailsAsync(expectedProduct.SKU, Arg.Any<CancellationToken>());
        await refresher.DidNotReceiveWithAnyArgs().RefreshOneAsync(default!, default);
    }

    [Fact]
    public async Task HandleAsync_RefreshesAndReturnsProduct_WhenMaterializedProductIsStale()
    {
        var reader = Substitute.For<IProductReadModelReader>();
        var refresher = Substitute.For<IProductMaterializationRefresher>();
        var freshnessPolicy = Substitute.For<IProductFreshnessPolicy>();
        var expectedProduct = CreateProductDetailsDto("PROD-001");
        var staleRefresh = new DateTime(2026, 10, 8, 8, 0, 0, DateTimeKind.Utc);
        var refreshedProduct = new MaterializedProductDetails(expectedProduct, DateTime.UtcNow);
        reader.GetDetailsAsync(expectedProduct.SKU, Arg.Any<CancellationToken>())
            .Returns(new MaterializedProductDetails(expectedProduct, staleRefresh), refreshedProduct);
        freshnessPolicy.GetRefreshDueAtUtcAsync(
                expectedProduct,
                staleRefresh,
                Arg.Any<CancellationToken>())
            .Returns(DateTime.MinValue);
        var handler = new GetProductDetailsHandler(reader, refresher, freshnessPolicy);

        var result = await handler.HandleAsync(new GetProductDetailsQuery(expectedProduct.SKU));

        Assert.Equal(expectedProduct, result);
        await refresher.Received(1).RefreshOneAsync(expectedProduct.SKU, Arg.Any<CancellationToken>());
        await reader.Received(2).GetDetailsAsync(expectedProduct.SKU, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_RefreshesAndReturnsProduct_WhenItIsNotMaterialized()
    {
        var reader = Substitute.For<IProductReadModelReader>();
        var refresher = Substitute.For<IProductMaterializationRefresher>();
        var freshnessPolicy = Substitute.For<IProductFreshnessPolicy>();
        var expectedProduct = CreateProductDetailsDto("PROD-001");
        var refreshedProduct = new MaterializedProductDetails(expectedProduct, DateTime.UtcNow);
        reader.GetDetailsAsync(expectedProduct.SKU, Arg.Any<CancellationToken>())
            .Returns((MaterializedProductDetails?)null, refreshedProduct);
        var handler = new GetProductDetailsHandler(reader, refresher, freshnessPolicy);

        var result = await handler.HandleAsync(new GetProductDetailsQuery(expectedProduct.SKU));

        Assert.Equal(expectedProduct, result);
        await refresher.Received(1).RefreshOneAsync(expectedProduct.SKU, Arg.Any<CancellationToken>());
        await reader.Received(2).GetDetailsAsync(expectedProduct.SKU, Arg.Any<CancellationToken>());
        await freshnessPolicy.DidNotReceiveWithAnyArgs()
            .GetRefreshDueAtUtcAsync(default!, default, default);
    }

    [Fact]
    public async Task HandleAsync_ReturnsNull_WhenProductDoesNotExistAfterRefreshing()
    {
        var reader = Substitute.For<IProductReadModelReader>();
        var refresher = Substitute.For<IProductMaterializationRefresher>();
        var freshnessPolicy = Substitute.For<IProductFreshnessPolicy>();
        const string sku = "NON-EXISTENT-SKU";
        reader.GetDetailsAsync(sku, Arg.Any<CancellationToken>())
            .Returns((MaterializedProductDetails?)null);
        var handler = new GetProductDetailsHandler(reader, refresher, freshnessPolicy);

        var result = await handler.HandleAsync(new GetProductDetailsQuery(sku));

        Assert.Null(result);
        await refresher.Received(1).RefreshOneAsync(sku, Arg.Any<CancellationToken>());
        await reader.Received(2).GetDetailsAsync(sku, Arg.Any<CancellationToken>());
    }

    private static ProductDetailsDto CreateProductDetailsDto(string productSymbol) => new(
        1,
        productSymbol,
        "Test product",
        "1234567890",
        new ProductTypeDto("product", "product"),
        true,
        null,
        7,
        [],
        [],
        [new ProductSupplierDto(10, "Supplier 1", "1234567890", true, "SUP-001", "Supplier Product")],
        [new ProductPriceDto("Detail", 10.00m, 12.30m)],
        [new ProductStockDto("WAR-A", 15m, 2m)]);
}
