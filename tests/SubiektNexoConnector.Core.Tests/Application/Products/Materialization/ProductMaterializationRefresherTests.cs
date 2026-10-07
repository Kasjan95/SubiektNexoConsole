using NSubstitute;
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.GetAdvancedFieldDefinitions;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Products;

namespace SubiektNexoConnector.Core.Tests.Application.Products.Materialization;

public sealed class ProductMaterializationRefresherTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly IAdditionalFieldDefinitionRepository _fieldDefinitions = Substitute.For<IAdditionalFieldDefinitionRepository>();
    private readonly IProductReadModelWriter _writer = Substitute.For<IProductReadModelWriter>();

    [Fact]
    public async Task RefreshOneAsync_UpsertsProductWithAdvancedFieldDefinitions_WhenProductExists()
    {
        var product = CreateProductDetails(1, "PROD-001");
        var definitions = CreateDefinitions();
        _products.GetDetails(product.SKU).Returns(product);
        _fieldDefinitions.GetAdvancedFieldDefinitions(Arg.Any<GetAdvancedFieldDefinitionsQuery>()).Returns(definitions);
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
        _fieldDefinitions.Received(1).GetAdvancedFieldDefinitions(
            Arg.Is<GetAdvancedFieldDefinitionsQuery>(query => query.Target == AdditionalFieldTarget.Product));
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
        _fieldDefinitions.DidNotReceiveWithAnyArgs().GetAdvancedFieldDefinitions(default!);
        await _writer.DidNotReceiveWithAnyArgs().UpsertAsync(default!, default!, default, default);
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
        _products.GetDetails(first.SKU).Returns(firstDetails);
        _products.GetDetails(second.SKU).Returns(secondDetails);
        _fieldDefinitions.GetAdvancedFieldDefinitions(Arg.Any<GetAdvancedFieldDefinitionsQuery>()).Returns(CreateDefinitions());
        var refresher = CreateRefresher();

        var result = await refresher.RefreshNextBatchAsync(10, 2);

        Assert.Equal(new ProductMaterializationBatchResult(2, second.Id, false), result);
        _products.Received(1).GetBatchAfterId(10, 3);
        _products.Received(1).GetDetails(first.SKU);
        _products.Received(1).GetDetails(second.SKU);
        _products.DidNotReceive().GetDetails(extra.SKU);
        await _writer.Received(1).UpsertAsync(firstDetails, Arg.Any<IReadOnlyDictionary<string, AdvancedFieldDefinitionDto>>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await _writer.Received(1).UpsertAsync(secondDetails, Arg.Any<IReadOnlyDictionary<string, AdvancedFieldDefinitionDto>>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        _fieldDefinitions.Received(1).GetAdvancedFieldDefinitions(Arg.Any<GetAdvancedFieldDefinitionsQuery>());
    }

    [Fact]
    public async Task RefreshNextBatchAsync_ReportsCompletedCycle_WhenLastBatchIsShorterThanBatchSize()
    {
        var product = CreateProductBasic(21, "PROD-021");
        var details = CreateProductDetails(product.Id, product.SKU);
        _products.GetBatchAfterId(20, 4).Returns([product]);
        _products.GetDetails(product.SKU).Returns(details);
        _fieldDefinitions.GetAdvancedFieldDefinitions(Arg.Any<GetAdvancedFieldDefinitionsQuery>()).Returns(CreateDefinitions());
        var refresher = CreateRefresher();

        var result = await refresher.RefreshNextBatchAsync(20, 3);

        Assert.Equal(new ProductMaterializationBatchResult(1, product.Id, true), result);
        await _writer.Received(1).UpsertAsync(details, Arg.Any<IReadOnlyDictionary<string, AdvancedFieldDefinitionDto>>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshNextBatchAsync_DeletesMissingProduct_AndAdvancesCursor()
    {
        var product = CreateProductBasic(31, "MISSING");
        _products.GetBatchAfterId(30, 2).Returns([product]);
        _products.GetDetails(product.SKU).Returns((ProductDetailsDto?)null);
        _fieldDefinitions.GetAdvancedFieldDefinitions(Arg.Any<GetAdvancedFieldDefinitionsQuery>()).Returns(CreateDefinitions());
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
        _fieldDefinitions.DidNotReceiveWithAnyArgs().GetAdvancedFieldDefinitions(default!);
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

    private ProductMaterializationRefresher CreateRefresher() => new(_products, _fieldDefinitions, _writer);

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
        [new AdvancedFieldDefinitionDto(
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
            null)]);
}
