using NSubstitute;
using SubiektNexoConnector.Core.Application.Products;
using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Core.Tests.Application.Products.Materialization;

public sealed class ProductFreshnessPolicyTests
{
    [Theory]
    [InlineData(1, 0, true)]
    [InlineData(0, 1, true)]
    [InlineData(0, 0, false)]
    public async Task GetMetadataAsync_SelectsPolicyFromTotalStock(
        decimal availableStock,
        decimal reservedStock,
        bool expectedToBeHot)
    {
        var expectedDefinition = expectedToBeHot
            ? MaterializationPolicyDefinitions.ProductsHot
            : MaterializationPolicyDefinitions.ProductsCold;
        var store = Substitute.For<IMaterializationPolicyStore>();
        var configuredLifetime = TimeSpan.FromMinutes(5);
        store.GetOrCreateAsync(
                expectedDefinition,
                Arg.Any<CancellationToken>())
            .Returns(new MaterializationPolicy(
                expectedDefinition.Id,
                expectedDefinition.Name,
                configuredLifetime,
                expectedDefinition.RefreshAhead,
                expectedDefinition.BatchSize,
                expectedDefinition.CandidateSelection,
                expectedDefinition.Requirements,
                new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        var policy = new ProductFreshnessPolicy(store);
        var refreshedAtUtc = DateTime.UtcNow;
        var product = CreateProduct(availableStock, reservedStock);

        var result = await policy.GetMetadataAsync(product, refreshedAtUtc);

        Assert.Equal(MaterializationStatus.Fresh, result.Status);
        Assert.Equal(refreshedAtUtc, result.LastSuccessAtUtc);
        await store.Received(1).GetOrCreateAsync(
            expectedDefinition,
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
