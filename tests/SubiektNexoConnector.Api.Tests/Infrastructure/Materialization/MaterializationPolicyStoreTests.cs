using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SubiektNexoConnector.Core.Materialization;
using SubiektNexoConnector.Infrastructure.Materialization.Persistence;
using SubiektNexoConnector.Infrastructure.Materialization.Persistence.Policies;

namespace SubiektNexoConnector.Api.Tests.Infrastructure.Materialization;

public sealed class MaterializationPolicyStoreTests
{
    [Fact]
    public async Task GetOrCreateAsync_CreatesPolicyWithDefaultLifetime_WhenItDoesNotExist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var store = new MaterializationPolicyStore(context);
        var beforeCreation = DateTime.UtcNow;

        var result = await store.GetOrCreateAsync(
            MaterializationPolicyKeys.ProductsHot,
            TimeSpan.FromSeconds(180));

        var afterCreation = DateTime.UtcNow;
        Assert.Equal(MaterializationPolicyKeys.ProductsHot, result.PolicyKey);
        Assert.Equal(TimeSpan.FromSeconds(180), result.Lifetime);
        Assert.InRange(result.UpdatedAtUtc, beforeCreation, afterCreation);
        var entity = await context.MaterializationPolicies.SingleAsync();
        Assert.Equal(180, entity.LifetimeSeconds);
    }

    [Fact]
    public async Task GetOrCreateAsync_ReturnsConfiguredLifetime_WithoutOverwritingIt()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var store = new MaterializationPolicyStore(context);
        var original = await store.GetOrCreateAsync(
            MaterializationPolicyKeys.ProductsCold,
            TimeSpan.FromSeconds(1800));

        var result = await store.GetOrCreateAsync(
            MaterializationPolicyKeys.ProductsCold,
            TimeSpan.FromSeconds(60));

        Assert.Equal(original, result);
        Assert.Equal(TimeSpan.FromSeconds(1800), result.Lifetime);
        Assert.Equal(1, await context.MaterializationPolicies.CountAsync());
    }

    [Fact]
    public async Task GetOrCreateAsync_RejectsNonPositiveDefaultLifetime()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var store = new MaterializationPolicyStore(context);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            store.GetOrCreateAsync(
                MaterializationPolicyKeys.ProductsHot,
                TimeSpan.Zero));
    }

    private static ReadModelDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ReadModelDbContext>()
            .UseSqlite(connection)
            .Options;

        return new ReadModelDbContext(options);
    }
}
