using NSubstitute;
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.Materialization;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Products;
using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Core.Tests.Application.Products.Materialization;

public sealed class ProductMaterializationRefresherTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly IAdvancedFieldDefinitionsMaterializationRefresher _fieldDefinitions = Substitute.For<IAdvancedFieldDefinitionsMaterializationRefresher>();
    private readonly IProductReadModelWriter _writer = Substitute.For<IProductReadModelWriter>();
    private readonly IProductMaterializationCandidateStore _candidateStore = Substitute.For<IProductMaterializationCandidateStore>();
    private readonly IMaterializationPolicyStore _policyStore = Substitute.For<IMaterializationPolicyStore>();

    [Fact]
    public async Task RefreshOneAsync_UpsertsProductWithAdvancedFieldDefinitions_WhenProductExists()
    {
        var product = CreateProductDetails(1, "PROD-001");
        var definitions = CreateDefinitions();
        _products.GetDetails(product.SKU).Returns(product);
        _fieldDefinitions.GetCurrentAsync(AdditionalFieldTarget.Product, Arg.Any<CancellationToken>()).Returns(definitions);
        var refresher = CreateRefresher();
        var startedAtUtc = DateTime.UtcNow;

        await refresher.RefreshOneAsync(product.SKU);

        var finishedAtUtc = DateTime.UtcNow;
        await _writer.Received(1).UpsertAsync(
            product,
            Arg.Is<IReadOnlyDictionary<string, AdvancedFieldDefinitionDto>>(fields =>
                fields.Count == 1 && fields["field-id"] == definitions.Fields.Single()),
            Arg.Is<DateTime>(refreshedAtUtc => refreshedAtUtc >= startedAtUtc && refreshedAtUtc <= finishedAtUtc),
            Arg.Any<CancellationToken>());
        await _fieldDefinitions.Received(1).GetCurrentAsync(AdditionalFieldTarget.Product, Arg.Any<CancellationToken>());
        await _writer.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default);
    }

    [Fact]
    public async Task RefreshOneAsync_DeletesMaterializedProduct_WhenProductDoesNotExist()
    {
        const string sku = "MISSING";
        _products.GetDetails(sku).Returns((ProductDetailsDto?)null);
        var refresher = CreateRefresher();

        await refresher.RefreshOneAsync(sku);

        await _writer.Received(1).DeleteAsync(sku, Arg.Any<CancellationToken>());
        await _fieldDefinitions.DidNotReceiveWithAnyArgs().GetCurrentAsync(default, default);
        await _writer.DidNotReceiveWithAnyArgs().UpsertAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task RefreshOneAsync_UsesAdvancedFieldDefinitionsFromGroups()
    {
        var definition = CreateDefinition();
        var product = CreateProductDetails(1, "PROD-001") with
        {
            AdvancedFields = [new AdditionalFieldValueDto(definition.Id, "value")]
        };
        _products.GetDetails(product.SKU).Returns(product);
        _fieldDefinitions.GetCurrentAsync(AdditionalFieldTarget.Product, Arg.Any<CancellationToken>()).Returns(
            new AdvancedFieldDefinitionsDto(
                AdditionalFieldTarget.Product,
                [new AdvancedFieldGroupDto("Group", 1, [definition])],
                []));
        var refresher = CreateRefresher();

        await refresher.RefreshOneAsync(product.SKU);

        await _writer.Received(1).UpsertAsync(
            product,
            Arg.Is<IReadOnlyDictionary<string, AdvancedFieldDefinitionDto>>(fields =>
                fields.Count == 1 && fields[definition.Id] == definition),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshOneAsync_ForcesDefinitionsRefreshWhenProductContainsUnknownField()
    {
        var definition = CreateDefinition();
        var product = CreateProductDetails(1, "PROD-001") with { AdvancedFields = [new AdditionalFieldValueDto(definition.Id, "value")] };
        var refreshedDefinitions = new AdvancedFieldDefinitionsDto(AdditionalFieldTarget.Product, [], [definition]);
        _products.GetDetails(product.SKU).Returns(product);
        _fieldDefinitions.GetCurrentAsync(AdditionalFieldTarget.Product, Arg.Any<CancellationToken>()).Returns(new AdvancedFieldDefinitionsDto(AdditionalFieldTarget.Product, [], []));
        _fieldDefinitions.RefreshAsync(AdditionalFieldTarget.Product, Arg.Any<CancellationToken>()).Returns(new AdvancedFieldDefinitionsMaterializationResult(refreshedDefinitions, true, true));
        var refresher = CreateRefresher();

        await refresher.RefreshOneAsync(product.SKU);

        await _fieldDefinitions.Received(1).RefreshAsync(AdditionalFieldTarget.Product, Arg.Any<CancellationToken>());
        await _writer.Received(1).UpsertAsync(product, Arg.Is<IReadOnlyDictionary<string, AdvancedFieldDefinitionDto>>(fields => fields.ContainsKey(definition.Id)), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshOneAsync_PropagatesCancellationTokenThroughMaterializationPipeline()
    {
        var product = CreateProductDetails(1, "PROD-001");
        var definitions = CreateDefinitions();
        using var cancellation = new CancellationTokenSource();
        var cancellationToken = cancellation.Token;
        _products.GetDetails(product.SKU, cancellationToken).Returns(product);
        _fieldDefinitions.GetCurrentAsync(AdditionalFieldTarget.Product, cancellationToken)
            .Returns(definitions);
        var refresher = CreateRefresher();

        await refresher.RefreshOneAsync(product.SKU, cancellationToken);

        _products.Received(1).GetDetails(product.SKU, cancellationToken);
        await _fieldDefinitions.Received(1).GetCurrentAsync(AdditionalFieldTarget.Product, cancellationToken);
        await _writer.Received(1).UpsertAsync(
            product,
            Arg.Any<IReadOnlyDictionary<string, AdvancedFieldDefinitionDto>>(),
            Arg.Any<DateTime>(),
            cancellationToken);
    }

    [Fact]
    public async Task RefreshNextBatchAsync_ProcessesOnlyBatchSizeProducts_AndReportsNextPage()
    {
        var first = CreateProductBasic(11, "PROD-011");
        var second = CreateProductBasic(12, "PROD-012");
        var extra = CreateProductBasic(13, "PROD-013");
        var firstDetails = CreateProductDetails(first.Id, first.SKU);
        var secondDetails = CreateProductDetails(second.Id, second.SKU);
        _products.GetBatchAfterId(10, 3).Returns([first, second, extra]);
        _products.GetDetailsBatch(
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>())
            .Returns([firstDetails, secondDetails]);
        _fieldDefinitions.GetCurrentAsync(AdditionalFieldTarget.Product, Arg.Any<CancellationToken>()).Returns(CreateDefinitions());
        var refresher = CreateRefresher();

        var result = await refresher.RefreshNextBatchAsync(10, 2);

        Assert.Equal(new ProductMaterializationBatchResult(2, second.Id, false), result);
        _products.Received(1).GetBatchAfterId(10, 3);
        _products.Received(1).GetDetailsBatch(
            Arg.Is<IReadOnlyCollection<string>>(skus => skus.SequenceEqual(new[] { first.SKU, second.SKU })),
            Arg.Any<CancellationToken>());
        await _writer.Received(1).UpsertBatchAsync(
            Arg.Is<IReadOnlyCollection<ProductDetailsDto>>(products =>
                products.SequenceEqual(new[] { firstDetails, secondDetails })),
            Arg.Any<IReadOnlyDictionary<string, AdvancedFieldDefinitionDto>>(),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
        await _fieldDefinitions.Received(1).GetCurrentAsync(AdditionalFieldTarget.Product, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshNextBatchAsync_ReportsCompletedCycle_WhenLastBatchIsShorterThanBatchSize()
    {
        var product = CreateProductBasic(21, "PROD-021");
        var details = CreateProductDetails(product.Id, product.SKU);
        _products.GetBatchAfterId(20, 4).Returns([product]);
        _products.GetDetailsBatch(
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>())
            .Returns([details]);
        _fieldDefinitions.GetCurrentAsync(AdditionalFieldTarget.Product, Arg.Any<CancellationToken>()).Returns(CreateDefinitions());
        var refresher = CreateRefresher();

        var result = await refresher.RefreshNextBatchAsync(20, 3);

        Assert.Equal(new ProductMaterializationBatchResult(1, product.Id, true), result);
        await _writer.Received(1).UpsertBatchAsync(
            Arg.Is<IReadOnlyCollection<ProductDetailsDto>>(products => products.SequenceEqual(new[] { details })),
            Arg.Any<IReadOnlyDictionary<string, AdvancedFieldDefinitionDto>>(),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshNextBatchAsync_DeletesMissingProduct_AndAdvancesCursor()
    {
        var product = CreateProductBasic(31, "MISSING");
        _products.GetBatchAfterId(30, 2).Returns([product]);
        _products.GetDetailsBatch(
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>())
            .Returns([]);
        _fieldDefinitions.GetCurrentAsync(AdditionalFieldTarget.Product, Arg.Any<CancellationToken>()).Returns(CreateDefinitions());
        var refresher = CreateRefresher();

        var result = await refresher.RefreshNextBatchAsync(30, 1);

        Assert.Equal(new ProductMaterializationBatchResult(1, product.Id, true), result);
        await _writer.Received(1).DeleteAsync(product.SKU, Arg.Any<CancellationToken>());
        await _writer.DidNotReceiveWithAnyArgs().UpsertAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task RefreshNextBatchAsync_ReturnsCompletedCycleWithoutLoadingDefinitions_WhenBatchIsEmpty()
    {
        _products.GetBatchAfterId(42, 6).Returns([]);
        var refresher = CreateRefresher();

        var result = await refresher.RefreshNextBatchAsync(42, 5);

        Assert.Equal(new ProductMaterializationBatchResult(0, 42, true), result);
        await _fieldDefinitions.DidNotReceiveWithAnyArgs().GetCurrentAsync(default, default);
        await _writer.DidNotReceiveWithAnyArgs().UpsertAsync(default!, default!, default, default);
        await _writer.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RefreshNextBatchAsync_Throws_WhenBatchSizeIsNotPositive(int batchSize)
    {
        var refresher = CreateRefresher();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => refresher.RefreshNextBatchAsync(null, batchSize));

        _products.DidNotReceiveWithAnyArgs().GetBatchAfterId(default, default);
    }

    [Fact]
    public async Task RefreshNextBatchAsync_RefreshesDueReadModelCandidates_SelectedByPolicy()
    {
        var definition = MaterializationPolicyDefinitions.ProductsHot;
        var policy = CreatePolicy(definition);
        var product = CreateProductDetails(1, "HOT-001");
        _policyStore.GetOrCreateAsync(definition, Arg.Any<CancellationToken>()).Returns(policy);
        _candidateStore.GetDueAsync(policy, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new ProductMaterializationCandidateBatch([product.SKU], false));
        _products.GetDetailsBatch(
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>())
            .Returns([product]);
        _fieldDefinitions.GetCurrentAsync(AdditionalFieldTarget.Product, Arg.Any<CancellationToken>()).Returns(CreateDefinitions());
        var refresher = CreateRefresher();

        var result = await refresher.RefreshNextBatchAsync(definition);

        Assert.Equal(new ProductMaterializationBatchResult(1, null, true), result);
        await _writer.Received(1).UpsertBatchAsync(
            Arg.Is<IReadOnlyCollection<ProductDetailsDto>>(products => products.SequenceEqual(new[] { product })),
            Arg.Any<IReadOnlyDictionary<string, AdvancedFieldDefinitionDto>>(),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
        _products.DidNotReceiveWithAnyArgs().GetBatchAfterId(default, default);
    }

    [Fact]
    public async Task RefreshNextBatchAsync_RefreshesOnlyProductsMissingFromReadModel()
    {
        var definition = MaterializationPolicyDefinitions.ProductsNew with { BatchSize = 2 };
        var policy = CreatePolicy(definition);
        var existing = new ProductMaterializationKey(11, "EXISTING");
        var missing = new ProductMaterializationKey(12, "NEW");
        var missingDetails = CreateProductDetails(missing.Id, missing.Sku);
        _policyStore.GetOrCreateAsync(definition, Arg.Any<CancellationToken>()).Returns(policy);
        _products.GetAllMaterializationCandidates().Returns([existing, missing]);
        _candidateStore.GetAllIdsAsync(Arg.Any<CancellationToken>())
            .Returns(new HashSet<int> { existing.Id });
        _products.GetDetailsBatch(
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>())
            .Returns([missingDetails]);
        _fieldDefinitions.GetCurrentAsync(AdditionalFieldTarget.Product, Arg.Any<CancellationToken>()).Returns(CreateDefinitions());
        var refresher = CreateRefresher();

        var result = await refresher.RefreshNextBatchAsync(definition);

        Assert.Equal(new ProductMaterializationBatchResult(1, null, true), result);
        _products.Received(1).GetAllMaterializationCandidates();
        _products.Received(1).GetDetailsBatch(
            Arg.Is<IReadOnlyCollection<string>>(skus => skus.SequenceEqual(new[] { missing.Sku })),
            Arg.Any<CancellationToken>());
        await _writer.Received(1).UpsertBatchAsync(
            Arg.Is<IReadOnlyCollection<ProductDetailsDto>>(products => products.SequenceEqual(new[] { missingDetails })),
            Arg.Any<IReadOnlyDictionary<string, AdvancedFieldDefinitionDto>>(),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshNextBatchAsync_LoadsSourceAndDefinitionsOnce_WhenNewProductsSpanMultipleBatches()
    {
        var definition = MaterializationPolicyDefinitions.ProductsNew with { BatchSize = 2 };
        var policy = CreatePolicy(definition);
        var products = new[]
        {
            new ProductMaterializationKey(11, "NEW-011"),
            new ProductMaterializationKey(12, "NEW-012"),
            new ProductMaterializationKey(13, "NEW-013")
        };
        _policyStore.GetOrCreateAsync(definition, Arg.Any<CancellationToken>()).Returns(policy);
        _products.GetAllMaterializationCandidates().Returns(products);
        _candidateStore.GetAllIdsAsync(Arg.Any<CancellationToken>()).Returns(new HashSet<int>());
        var details = products
            .Select(product => CreateProductDetails(product.Id, product.Sku))
            .ToArray();
        _products.GetDetailsBatch(
                Arg.Is<IReadOnlyCollection<string>>(skus => skus.SequenceEqual(products.Take(2).Select(product => product.Sku))),
                Arg.Any<CancellationToken>())
            .Returns(details.Take(2).ToArray());
        _products.GetDetailsBatch(
                Arg.Is<IReadOnlyCollection<string>>(skus => skus.SequenceEqual(products.Skip(2).Select(product => product.Sku))),
                Arg.Any<CancellationToken>())
            .Returns(details.Skip(2).ToArray());
        _fieldDefinitions.GetCurrentAsync(AdditionalFieldTarget.Product, Arg.Any<CancellationToken>()).Returns(CreateDefinitions());
        var refresher = CreateRefresher();

        var result = await refresher.RefreshNextBatchAsync(definition);

        Assert.Equal(new ProductMaterializationBatchResult(3, null, true), result);
        _products.Received(1).GetAllMaterializationCandidates();
        await _fieldDefinitions.Received(1).GetCurrentAsync(AdditionalFieldTarget.Product, Arg.Any<CancellationToken>());
        _products.Received(2).GetDetailsBatch(
            Arg.Any<IReadOnlyCollection<string>>(),
            Arg.Any<CancellationToken>());
        await _writer.Received(2).UpsertBatchAsync(
            Arg.Any<IReadOnlyCollection<ProductDetailsDto>>(),
            Arg.Any<IReadOnlyDictionary<string, AdvancedFieldDefinitionDto>>(),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
    }

    private ProductMaterializationRefresher CreateRefresher() => new(
        _products,
        _fieldDefinitions,
        _writer,
        _candidateStore,
        _policyStore);

    private static MaterializationPolicy CreatePolicy(
        MaterializationPolicyDefinition definition) => new(
        definition.Id,
        definition.Name,
        definition.Lifetime,
        definition.RefreshAhead,
        definition.BatchSize,
        definition.CandidateSelection,
        definition.Requirements,
        DateTime.UtcNow);

    private static ProductBasicDto CreateProductBasic(int id, string sku) => new(id, sku, $"Product {id}", null);

    private static ProductDetailsDto CreateProductDetails(int id, string sku) => new(
        id,
        sku,
        $"Product {id}",
        null,
        new ProductTypeDto("product", "Product"),
        true,
        null,
        null,
        [],
        [],
        [],
        [],
        []);

    private static AdvancedFieldDefinitionsDto CreateDefinitions() => new(
        AdditionalFieldTarget.Product,
        [],
        [CreateDefinition()]);

    private static AdvancedFieldDefinitionDto CreateDefinition() => new(
        "field-id",
        "Field",
        null,
        AdvancedFieldDataType.Text,
        false,
        true,
        true,
        true,
        null,
        null,
        null,
        null,
        null);
}
