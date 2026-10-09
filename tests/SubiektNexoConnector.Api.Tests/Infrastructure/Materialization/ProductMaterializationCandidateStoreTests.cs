using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.Products;
using SubiektNexoConnector.Core.Materialization;
using SubiektNexoConnector.Infrastructure.Materialization.Persistence;
using SubiektNexoConnector.Infrastructure.Materialization.Products;

namespace SubiektNexoConnector.Api.Tests.Infrastructure.Materialization;

public sealed class ProductMaterializationCandidateStoreTests
{
    [Fact]
    public async Task GetDueAsync_ReturnsOnlyDueProductsMatchingStockRequirement()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var writer = new ProductReadModelWriter(context);
        var utcNow = new DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc);

        await writer.UpsertAsync(CreateProduct(1, "HOT-DUE", 1), EmptyDefinitions, utcNow.AddMinutes(-4));
        await writer.UpsertAsync(CreateProduct(2, "HOT-FRESH", 1), EmptyDefinitions, utcNow.AddMinutes(-1));
        await writer.UpsertAsync(CreateProduct(3, "COLD-DUE", 0), EmptyDefinitions, utcNow.AddHours(-1));
        context.ChangeTracker.Clear();
        var store = new ProductMaterializationCandidateStore(context);

        var result = await store.GetDueAsync(
            CreatePolicy(MaterializationPolicyDefinitions.ProductsHot),
            utcNow);

        Assert.Equal(["HOT-DUE"], result.Skus);
        Assert.False(result.HasMore);
    }

    [Fact]
    public async Task GetDueAsync_AppliesTextContainsAndDoesNotContainRequirements()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var writer = new ProductReadModelWriter(context);
        var utcNow = new DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc);

        await writer.UpsertAsync(CreateProduct(1, "KEEP-001", 0), EmptyDefinitions, utcNow.AddHours(-1));
        await writer.UpsertAsync(CreateProduct(2, "KEEP-EXCLUDE", 0), EmptyDefinitions, utcNow.AddHours(-1));
        await writer.UpsertAsync(CreateProduct(3, "OTHER-001", 0), EmptyDefinitions, utcNow.AddHours(-1));
        context.ChangeTracker.Clear();
        var definition = MaterializationPolicyDefinitions.ProductsCold with
        {
            Requirements =
            [
                new MaterializationRequirement(
                    MaterializationFieldIds.ProductSku,
                    MaterializationComparisonOperator.Contains,
                    TextValue: "KEEP"),
                new MaterializationRequirement(
                    MaterializationFieldIds.ProductSku,
                    MaterializationComparisonOperator.DoesNotContain,
                    TextValue: "EXCLUDE")
            ]
        };
        var store = new ProductMaterializationCandidateStore(context);

        var result = await store.GetDueAsync(CreatePolicy(definition), utcNow);

        Assert.Equal(["KEEP-001"], result.Skus);
    }

    [Fact]
    public async Task GetAllIdsAsync_ReturnsAllReadModelProductIds()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var writer = new ProductReadModelWriter(context);

        await writer.UpsertAsync(CreateProduct(1, "ONE", 0), EmptyDefinitions, DateTime.UtcNow);
        await writer.UpsertAsync(CreateProduct(3, "THREE", 0), EmptyDefinitions, DateTime.UtcNow);
        context.ChangeTracker.Clear();
        var store = new ProductMaterializationCandidateStore(context);

        var result = await store.GetAllIdsAsync();

        Assert.True(result.SetEquals([1, 3]));
    }

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

    private static ProductDetailsDto CreateProduct(
        int id,
        string sku,
        decimal totalStock) => new(
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
        [new ProductStockDto("MAIN", totalStock, 0)]);

    private static ReadModelDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ReadModelDbContext>()
            .UseSqlite(connection)
            .Options;

        return new ReadModelDbContext(options);
    }

    private static IReadOnlyDictionary<string, AdvancedFieldDefinitionDto> EmptyDefinitions { get; } =
        new Dictionary<string, AdvancedFieldDefinitionDto>();
}
