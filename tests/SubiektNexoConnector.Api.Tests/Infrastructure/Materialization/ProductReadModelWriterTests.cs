using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Common;
using SubiektNexoConnector.Core.Application.Products;
using SubiektNexoConnector.Infrastructure.Materialization.Persistence;
using SubiektNexoConnector.Infrastructure.Materialization.Products;

namespace SubiektNexoConnector.Api.Tests.Infrastructure.Materialization;

public sealed class ProductReadModelWriterTests
{
    [Fact]
    public async Task UpsertAsync_PersistsAndSynchronizesCompleteProduct()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var writer = new ProductReadModelWriter(context);
        var definitions = CreateDefinitions();
        var dictionaryGuid = Guid.Parse("be23de79-7355-4e82-a52e-62fc73df3bc7");
        var firstRefresh = new DateTime(2026, 10, 2, 8, 0, 0, DateTimeKind.Utc);

        await writer.UpsertAsync(
            CreateProduct(
                name: "Original product",
                basicFields: [new("PoleWlasne1", "basic value")],
                advancedFields:
                [
                    new("text", "text value"),
                    new("decimal", 12.34m),
                    new("dictionary-int", 100012),
                    new("dictionary-guid", dictionaryGuid)
                ],
                suppliers:
                [
                    new(10, "Supplier A", "111", true, "A-1", "Product A"),
                    new(20, "Supplier B", null, false, null, null)
                ],
                prices:
                [
                    new("Retail", 10m, 12.30m),
                    new("Wholesale", 8m, 9.84m)
                ],
                stocks: [new("MAIN", 5m, 1m)]),
            definitions,
            firstRefresh);

        Assert.Single(await context.Products.ToListAsync());
        Assert.Single(await context.ProductBasicFields.ToListAsync());
        Assert.Equal(4, await context.ProductAdvancedFields.CountAsync());
        Assert.Equal(2, await context.ProductPrices.CountAsync());
        Assert.Single(await context.ProductStocks.ToListAsync());
        Assert.Equal(2, await context.ProductSuppliers.CountAsync());

        context.ChangeTracker.Clear();
        var secondRefresh = firstRefresh.AddMinutes(5);

        await writer.UpsertAsync(
            CreateProduct(
                name: "Updated product",
                basicFields: [new("PoleWlasne2", "new basic value")],
                advancedFields:
                [
                    new("decimal", 99.50m),
                    new("dictionary-int", 100013)
                ],
                suppliers: [new(10, "Supplier A updated", "222", true, "A-2", null)],
                prices: [new("Retail", 20m, 24.60m)],
                stocks: [new("SECONDARY", 7m, 2m)]),
            definitions,
            secondRefresh);

        context.ChangeTracker.Clear();

        var product = await context.Products.SingleAsync();
        var basicFields = await context.ProductBasicFields.SingleAsync();
        var price = await context.ProductPrices.SingleAsync();
        var stock = await context.ProductStocks.SingleAsync();
        var supplier = await context.ProductSuppliers.SingleAsync();
        var advancedFields = await context.ProductAdvancedFields
            .ToDictionaryAsync(field => field.FieldId);

        Assert.Equal("Updated product", product.Name);
        Assert.Equal(secondRefresh, product.RefreshedAtUtc);
        Assert.Null(basicFields.Field1);
        Assert.Equal("new basic value", basicFields.Field2);
        Assert.Equal(0b00000010, basicFields.PresentFieldsMask);
        Assert.Equal("Retail", price.PriceLevelName);
        Assert.Equal(20m, price.PriceNet);
        Assert.Equal("SECONDARY", stock.WarehouseSymbol);
        Assert.Equal("Supplier A updated", supplier.Name);
        Assert.Equal(2, advancedFields.Count);
        Assert.Equal(99.50m, advancedFields["decimal"].DecimalValue);
        Assert.Equal(100013, advancedFields["dictionary-int"].DictionaryIntegerKey);
        Assert.DoesNotContain("text", advancedFields);
        Assert.DoesNotContain("dictionary-guid", advancedFields);
    }

    [Fact]
    public async Task DeleteAsync_RemovesProductAndAllDependentData()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var writer = new ProductReadModelWriter(context);

        await writer.UpsertAsync(
            CreateProduct(
                name: "Product to delete",
                basicFields: [new("PoleWlasne1", "basic value")],
                advancedFields: [new("text", "text value")],
                suppliers: [new(10, "Supplier", null, true, null, null)],
                prices: [new("Retail", 10m, 12.30m)],
                stocks: [new("MAIN", 5m, 1m)]),
            CreateDefinitions(),
            new DateTime(2026, 10, 7, 8, 0, 0, DateTimeKind.Utc));
        context.ChangeTracker.Clear();

        await writer.DeleteAsync("SKU-1");

        Assert.Empty(await context.Products.ToListAsync());
        Assert.Empty(await context.ProductBasicFields.ToListAsync());
        Assert.Empty(await context.ProductAdvancedFields.ToListAsync());
        Assert.Empty(await context.ProductPrices.ToListAsync());
        Assert.Empty(await context.ProductStocks.ToListAsync());
        Assert.Empty(await context.ProductSuppliers.ToListAsync());
    }

    [Fact]
    public async Task UpsertBatchAsync_PersistsAllProductsInOneBatch()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var writer = new ProductReadModelWriter(context);
        var first = CreateProduct(
            name: "First product",
            basicFields: [],
            advancedFields: [],
            suppliers: [],
            prices: [new("Retail", 10m, 12.30m)],
            stocks: [new("MAIN", 5m, 0m)]);
        var second = first with
        {
            Id = 2,
            SKU = "SKU-2",
            Name = "Second product"
        };
        var refreshedAtUtc = new DateTime(2026, 10, 8, 8, 0, 0, DateTimeKind.Utc);

        await writer.UpsertBatchAsync(
            [first, second],
            CreateDefinitions(),
            refreshedAtUtc);

        Assert.Equal(2, await context.Products.CountAsync());
        Assert.Equal(2, await context.ProductBasicFields.CountAsync());
        Assert.Equal(2, await context.ProductPrices.CountAsync());
        Assert.Equal(2, await context.ProductStocks.CountAsync());
        Assert.All(
            await context.Products.AsNoTracking().ToListAsync(),
            product => Assert.Equal(refreshedAtUtc, product.RefreshedAtUtc));
    }

    private static ReadModelDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ReadModelDbContext>()
            .UseSqlite(connection)
            .Options;
        return new ReadModelDbContext(options);
    }

    private static ProductDetailsDto CreateProduct(
        string name,
        IReadOnlyCollection<AdditionalFieldValueDto> basicFields,
        IReadOnlyCollection<AdditionalFieldValueDto> advancedFields,
        IReadOnlyCollection<ProductSupplierDto> suppliers,
        IReadOnlyCollection<ProductPriceDto> prices,
        IReadOnlyCollection<ProductStockDto> stocks) => new(
        1,
        "SKU-1",
        name,
        "5900000000001",
        new ProductTypeDto("towar", "Towar"),
        true,
        new FlagAssignmentDto(3, "Important"),
        2,
        basicFields,
        advancedFields,
        suppliers,
        prices,
        stocks);

    private static IReadOnlyDictionary<string, AdvancedFieldDefinitionDto> CreateDefinitions()
    {
        var definitions = new[]
        {
            CreateDefinition("text", AdvancedFieldDataType.Text),
            CreateDefinition("decimal", AdvancedFieldDataType.Decimal),
            CreateDefinition("dictionary-int", AdvancedFieldDataType.Dictionary, "int"),
            CreateDefinition("dictionary-guid", AdvancedFieldDataType.Dictionary, "guid")
        };

        return definitions.ToDictionary(definition => definition.Id, StringComparer.Ordinal);
    }

    private static AdvancedFieldDefinitionDto CreateDefinition(
        string id,
        AdvancedFieldDataType dataType,
        string? dictionaryKeyType = null) => new(
        id,
        id,
        null,
        dataType,
        false,
        true,
        true,
        false,
        null,
        null,
        null,
        null,
        dictionaryKeyType is null
            ? null
            : new AdvancedFieldDictionaryDto(
                AdvancedFieldDictionaryKind.Custom,
                dictionaryKeyType,
                null,
                null));
}
