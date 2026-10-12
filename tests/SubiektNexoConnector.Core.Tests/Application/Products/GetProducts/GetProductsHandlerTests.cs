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
        var policyStore = Substitute.For<IMaterializationPolicyStore>();
        var expectedProducts = new List<ProductBasicDto>
        {
            new(1, "Prod.1.SKU", "Product 1 Name", "1234567890"),
            new(2, "Prod.2.SKU", "Product 2 Name", null)
        };
        var query = new GetProductsQuery();
        ConfigureCompletedDiscovery(checkpointStore, policyStore, DateTime.UtcNow);
        reader.GetAllAsync(query, Arg.Any<CancellationToken>()).Returns(expectedProducts);
        var handler = new GetProductsHandler(reader, checkpointStore, policyStore);

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
        var policyStore = Substitute.For<IMaterializationPolicyStore>();
        var query = new GetProductsQuery();
        checkpointStore.GetOrCreateAsync(MaterializationPolicyDefinitions.ProductsNew.Id, Arg.Any<CancellationToken>()).Returns(CreateCheckpoint(null));
        var handler = new GetProductsHandler(reader, checkpointStore, policyStore);

        var result = await handler.HandleAsync(query);

        Assert.Null(result.Value);
        Assert.Equal(MaterializationStatus.Unavailable, result.Metadata.Status);
        await reader.DidNotReceive().GetAllAsync(Arg.Any<GetProductsQuery>(), Arg.Any<CancellationToken>());
        await policyStore.DidNotReceive().GetOrCreateAsync(Arg.Any<MaterializationPolicyDefinition>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ReturnsProductsAndStaleMetadata_WhenDiscoveryCheckpointIsExpired()
    {
        var reader = Substitute.For<IProductReadModelReader>();
        var checkpointStore = Substitute.For<IMaterializationCheckpointStore>();
        var policyStore = Substitute.For<IMaterializationPolicyStore>();
        var query = new GetProductsQuery();
        ConfigureCompletedDiscovery(checkpointStore, policyStore, DateTime.UtcNow.AddHours(-1));
        reader.GetAllAsync(query, Arg.Any<CancellationToken>()).Returns([]);
        var handler = new GetProductsHandler(reader, checkpointStore, policyStore);

        var result = await handler.HandleAsync(query);

        Assert.NotNull(result.Value);
        Assert.Empty(result.Value);
        Assert.Equal(MaterializationStatus.Stale, result.Metadata.Status);
    }

    private static void ConfigureCompletedDiscovery(IMaterializationCheckpointStore checkpointStore, IMaterializationPolicyStore policyStore, DateTime completedAtUtc)
    {
        var definition = MaterializationPolicyDefinitions.ProductsNew;
        checkpointStore.GetOrCreateAsync(definition.Id, Arg.Any<CancellationToken>()).Returns(CreateCheckpoint(completedAtUtc));
        policyStore.GetOrCreateAsync(definition, Arg.Any<CancellationToken>()).Returns(new MaterializationPolicy(definition.Id, definition.Name, definition.Lifetime, definition.RefreshAhead, definition.BatchSize, definition.CandidateSelection, definition.Requirements, DateTime.UtcNow));
    }

    private static MaterializationCheckpoint CreateCheckpoint(DateTime? completedAtUtc) => new(MaterializationPolicyDefinitions.ProductsNew.Id, null, null, null, completedAtUtc, null, null);
}
