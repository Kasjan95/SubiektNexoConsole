using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Common;
using SubiektNexoConnector.Core.Application.Products;
using SubiektNexoConnector.Infrastructure.Materialization.Persistence;
using SubiektNexoConnector.Infrastructure.Materialization.Products;

namespace SubiektNexoConnector.Api.Tests.Infrastructure.Materialization;

public sealed class ProductReadModelReaderTests
{
    [Fact]
    public async Task GetDetailsAsync_ReturnsCompleteProductPersistedByWriter()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var writer = new ProductReadModelWriter(context);
        var reader = new ProductReadModelReader(context);
        var product = CreateCompleteProduct();
        var refreshedAtUtc = new DateTime(2026, 10, 6, 8, 0, 0, DateTimeKind.Utc);

        await writer.UpsertAsync(
            product,
            CreateDefinitions(),
            refreshedAtUtc);
        context.ChangeTracker.Clear();

        var result = await reader.GetDetailsAsync(product.SKU);

        Assert.NotNull(result);
        Assert.Equal(refreshedAtUtc, result.RefreshedAtUtc);
        Assert.Equal(product.Id, result.Product.Id);
        Assert.Equal(product.SKU, result.Product.SKU);
        Assert.Equal(product.Name, result.Product.Name);
        Assert.Equal(product.EAN, result.Product.EAN);
        Assert.Equal(product.Type, result.Product.Type);
        Assert.Equal(product.IsActive, result.Product.IsActive);
        Assert.Equal(product.Flag, result.Product.Flag);
        Assert.Equal(product.SupplierLeadTimeDays, result.Product.SupplierLeadTimeDays);
        Assert.Equal(product.BasicFields.ToArray(), result.Product.BasicFields.ToArray());

        foreach (var expectedField in product.AdvancedFields)
        {
            var actualField = Assert.Single(
                result.Product.AdvancedFields,
                field => field.Id == expectedField.Id);
            Assert.Equal(expectedField.Value, actualField.Value);
        }

        Assert.Equal(
            product.DefaultSuppliers.OrderByDescending(supplier => supplier.IsPrimary).ThenBy(supplier => supplier.Id).ToArray(),
            result.Product.DefaultSuppliers.ToArray());
        Assert.Equal(
            product.Prices.OrderBy(price => price.PriceLevelName, StringComparer.Ordinal).ToArray(),
            result.Product.Prices.ToArray());
        Assert.Equal(
            product.Stocks.OrderBy(stock => stock.WarehouseSymbol, StringComparer.Ordinal).ToArray(),
            result.Product.Stocks.ToArray());
    }

    [Fact]
    public async Task GetAllAsync_FiltersOrdersAndPaginatesProducts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var writer = new ProductReadModelWriter(context);
        var reader = new ProductReadModelReader(context);
        var refreshedAtUtc = new DateTime(2026, 10, 6, 8, 0, 0, DateTimeKind.Utc);

        await writer.UpsertAsync(CreateListProduct(3, "SKU-3", "Target product", "333"), EmptyDefinitions, refreshedAtUtc);
        await writer.UpsertAsync(CreateListProduct(1, "SKU-1", "First product", "111"), EmptyDefinitions, refreshedAtUtc);
        await writer.UpsertAsync(CreateListProduct(2, "SKU-2", "Second product", "222"), EmptyDefinitions, refreshedAtUtc);
        context.ChangeTracker.Clear();

        var secondPage = await reader.GetAllAsync(new GetProductsQuery { Page = 2, PageSize = 1 });
        var searchResult = await reader.GetAllAsync(new GetProductsQuery { Search = "Target", Page = 1, PageSize = 10 });

        Assert.Equal(2, Assert.Single(secondPage).Id);
        Assert.Equal(3, Assert.Single(searchResult).Id);
    }

    [Fact]
    public async Task GetDetailsAsync_ReturnsNullWhenProductDoesNotExist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var reader = new ProductReadModelReader(context);

        var result = await reader.GetDetailsAsync("missing");

        Assert.Null(result);
    }

    private static ReadModelDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ReadModelDbContext>()
            .UseSqlite(connection)
            .Options;
        return new ReadModelDbContext(options);
    }

    private static ProductDetailsDto CreateCompleteProduct()
    {
        var date = new DateTime(2026, 10, 6, 7, 30, 0, DateTimeKind.Utc);
        var guid = Guid.Parse("fb765cd8-1d36-4557-97a4-3ef1f37fc9db");
        var dictionaryGuid = Guid.Parse("4840348d-fb44-4ec0-b155-abf317985606");

        return new ProductDetailsDto(
            1,
            "SKU-1",
            "Complete product",
            "5900000000001",
            new ProductTypeDto("towar", "Towar"),
            true,
            new FlagAssignmentDto(3, "Important"),
            2,
            [
                new AdditionalFieldValueDto("PoleWlasne1", "first"),
                new AdditionalFieldValueDto("PoleWlasne3", null),
                new AdditionalFieldValueDto("PoleWlasne8", "last")
            ],
            [
                new AdditionalFieldValueDto("boolean", true),
                new AdditionalFieldValueDto("date", date),
                new AdditionalFieldValueDto("decimal", 12.34m),
                new AdditionalFieldValueDto("dictionary-guid", dictionaryGuid),
                new AdditionalFieldValueDto("dictionary-int", 100012),
                new AdditionalFieldValueDto("guid", guid),
                new AdditionalFieldValueDto("integer", 42),
                new AdditionalFieldValueDto("long-text", "long value"),
                new AdditionalFieldValueDto("text", "text value")
            ],
            [
                new ProductSupplierDto(20, "Supplier B", null, false, null, null),
                new ProductSupplierDto(10, "Supplier A", "111", true, "A-1", "Product A")
            ],
            [
                new ProductPriceDto("Wholesale", 8m, 9.84m),
                new ProductPriceDto("Retail", 10m, 12.30m)
            ],
            [
                new ProductStockDto("SECONDARY", 7m, 2m),
                new ProductStockDto("MAIN", 5m, 1m)
            ]);
    }

    private static ProductDetailsDto CreateListProduct(
        int id,
        string sku,
        string name,
        string ean) => new(
        id,
        sku,
        name,
        ean,
        new ProductTypeDto("towar", "Towar"),
        true,
        null,
        null,
        [],
        [],
        [],
        [],
        []);

    private static IReadOnlyDictionary<string, AdvancedFieldDefinitionDto> CreateDefinitions()
    {
        var definitions = new[]
        {
            CreateDefinition("boolean", AdvancedFieldDataType.Boolean),
            CreateDefinition("date", AdvancedFieldDataType.Date),
            CreateDefinition("decimal", AdvancedFieldDataType.Decimal),
            CreateDefinition("dictionary-guid", AdvancedFieldDataType.Dictionary, "guid"),
            CreateDefinition("dictionary-int", AdvancedFieldDataType.Dictionary, "int"),
            CreateDefinition("guid", AdvancedFieldDataType.Guid),
            CreateDefinition("integer", AdvancedFieldDataType.Integer),
            CreateDefinition("long-text", AdvancedFieldDataType.LongText),
            CreateDefinition("text", AdvancedFieldDataType.Text)
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

    private static IReadOnlyDictionary<string, AdvancedFieldDefinitionDto> EmptyDefinitions { get; } =
        new Dictionary<string, AdvancedFieldDefinitionDto>();
}
