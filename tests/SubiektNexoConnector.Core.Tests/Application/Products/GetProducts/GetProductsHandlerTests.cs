using NSubstitute;
using SubiektNexoConnector.Core.Application.Products;
using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Core.Tests.Application.Products;

public class GetProductsHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsProductsAndFreshMetadata_WhenInitialDiscoveryIsComplete()
    {
        var reader = Substitute.For<IProductReadModelReader>();
        var checkpointStore = Substitute.For<IMaterializationCheckpointStore>();
        var freshnessEvaluator = Substitute.For<IMaterializationFreshnessEvaluator>();
        var expectedProducts = new List<ProductBasicDto>
        {
            new(1, "Prod.1.SKU", "Product 1 Name", "1234567890"),
            new(2, "Prod.2.SKU", "Product 2 Name", null)
        };
        var query = new GetProductsQuery();
        ConfigureCompletedDiscovery(checkpointStore, freshnessEvaluator, DateTime.UtcNow, MaterializationStatus.Fresh);
        reader.GetAllAsync(query, Arg.Any<CancellationToken>()).Returns(expectedProducts);
        var handler = new GetProductsHandler(reader, checkpointStore, freshnessEvaluator);

        var result = await handler.HandleAsync(query);

        Assert.Equal(expectedProducts, result.Value);
        Assert.Equal(MaterializationStatus.Fresh, result.Metadata.Status);
        await reader.Received(1).GetAllAsync(query, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ReturnsUnavailableWithoutReadingProducts_WhenInitialDiscoveryIsIncomplete()
    {
        var reader = Substitute.For<IProductReadModelReader>();
        var checkpointStore = Substitute.For<IMaterializationCheckpointStore>();
        var freshnessEvaluator = Substitute.For<IMaterializationFreshnessEvaluator>();
        var query = new GetProductsQuery();
        checkpointStore.GetOrCreateAsync(MaterializationPolicyDefinitions.ProductsNew.Id, Arg.Any<CancellationToken>()).Returns(CreateCheckpoint(null));
        var handler = new GetProductsHandler(reader, checkpointStore, freshnessEvaluator);

        var result = await handler.HandleAsync(query);

        Assert.Null(result.Value);
        Assert.Equal(MaterializationStatus.Unavailable, result.Metadata.Status);
        await reader.DidNotReceive().GetAllAsync(Arg.Any<GetProductsQuery>(), Arg.Any<CancellationToken>());
        await freshnessEvaluator.DidNotReceiveWithAnyArgs().EvaluateAsync(default!, default, default);
    }

    [Fact]
    public async Task HandleAsync_ReturnsProductsAndStaleMetadata_WhenDiscoveryCheckpointIsExpired()
    {
        var reader = Substitute.For<IProductReadModelReader>();
        var checkpointStore = Substitute.For<IMaterializationCheckpointStore>();
        var freshnessEvaluator = Substitute.For<IMaterializationFreshnessEvaluator>();
        var query = new GetProductsQuery();
        ConfigureCompletedDiscovery(checkpointStore, freshnessEvaluator, DateTime.UtcNow.AddHours(-1), MaterializationStatus.Stale);
        reader.GetAllAsync(query, Arg.Any<CancellationToken>()).Returns([]);
        var handler = new GetProductsHandler(reader, checkpointStore, freshnessEvaluator);

        var result = await handler.HandleAsync(query);

        Assert.NotNull(result.Value);
        Assert.Empty(result.Value);
        Assert.Equal(MaterializationStatus.Stale, result.Metadata.Status);
    }

    private static void ConfigureCompletedDiscovery(IMaterializationCheckpointStore checkpointStore, IMaterializationFreshnessEvaluator freshnessEvaluator, DateTime completedAtUtc, MaterializationStatus status)
    {
        var definition = MaterializationPolicyDefinitions.ProductsNew;
        checkpointStore.GetOrCreateAsync(definition.Id, Arg.Any<CancellationToken>()).Returns(CreateCheckpoint(completedAtUtc));
        freshnessEvaluator.EvaluateAsync(definition, completedAtUtc, Arg.Any<CancellationToken>()).Returns(new MaterializationMetadata(status, completedAtUtc));
    }

    private static MaterializationCheckpoint CreateCheckpoint(DateTime? completedAtUtc) => new(MaterializationPolicyDefinitions.ProductsNew.Id, null, null, null, completedAtUtc, null, null);
}
