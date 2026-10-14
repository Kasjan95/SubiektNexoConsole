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
        var evaluator = Substitute.For<IMaterializationFreshnessEvaluator>();
        var refreshedAtUtc = DateTime.UtcNow;
        evaluator.EvaluateAsync(expectedDefinition, refreshedAtUtc, Arg.Any<CancellationToken>()).Returns(new MaterializationMetadata(MaterializationStatus.Fresh, refreshedAtUtc));
        var policy = new ProductFreshnessPolicy(evaluator);
        var product = CreateProduct(availableStock, reservedStock);

        var result = await policy.GetMetadataAsync(product, refreshedAtUtc);

        Assert.Equal(MaterializationStatus.Fresh, result.Status);
        Assert.Equal(refreshedAtUtc, result.LastSuccessAtUtc);
        await evaluator.Received(1).EvaluateAsync(expectedDefinition, refreshedAtUtc, Arg.Any<CancellationToken>());
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
