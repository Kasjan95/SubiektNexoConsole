using NSubstitute;
using SubiektNexoConnector.Core.Application.Products;
using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Core.Tests.Application.Products.Materialization;

public sealed class ProductFreshnessPolicyTests
{
    [Theory]
    [InlineData(1, 0, MaterializationPolicyKeys.ProductsHot, 180)]
    [InlineData(0, 1, MaterializationPolicyKeys.ProductsHot, 180)]
    [InlineData(0, 0, MaterializationPolicyKeys.ProductsCold, 1800)]
    public async Task GetRefreshDueAtUtcAsync_SelectsPolicyFromTotalStock(
        decimal availableStock,
        decimal reservedStock,
        string expectedPolicyKey,
        int expectedDefaultLifetimeSeconds)
    {
        var store = Substitute.For<IMaterializationPolicyStore>();
        var configuredLifetime = TimeSpan.FromSeconds(45);
        store.GetOrCreateAsync(
                expectedPolicyKey,
                TimeSpan.FromSeconds(expectedDefaultLifetimeSeconds),
                Arg.Any<CancellationToken>())
            .Returns(new MaterializationPolicy(
                expectedPolicyKey,
                configuredLifetime,
                new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        var policy = new ProductFreshnessPolicy(store);
        var refreshedAtUtc = new DateTime(2026, 10, 8, 8, 0, 0, DateTimeKind.Utc);
        var product = CreateProduct(availableStock, reservedStock);

        var result = await policy.GetRefreshDueAtUtcAsync(product, refreshedAtUtc);

        Assert.Equal(refreshedAtUtc.Add(configuredLifetime), result);
        await store.Received(1).GetOrCreateAsync(
            expectedPolicyKey,
            TimeSpan.FromSeconds(expectedDefaultLifetimeSeconds),
            Arg.Any<CancellationToken>());
    }

    private static ProductDetailsDto CreateProduct(
        decimal availableStock,
        decimal reservedStock) => new(
        1,
        "PROD-001",
        "Test product",
        null,
        new ProductTypeDto("product", "product"),
        true,
        null,
        null,
        [],
        [],
        [],
        [],
        [new ProductStockDto("MAIN", availableStock, reservedStock)]);
}
