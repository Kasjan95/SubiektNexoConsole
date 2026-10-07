using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SubiektNexoConnector.Core.Materialization;
using SubiektNexoConnector.Infrastructure.Materialization.Persistence;
using SubiektNexoConnector.Infrastructure.Materialization.Persistence.States;

namespace SubiektNexoConnector.Api.Tests.Infrastructure.Materialization;

public sealed class MaterializationCheckpointStoreTests
{
    [Fact]
    public async Task GetOrCreateAsync_CreatesEmptyCheckpoint_WhenItDoesNotExist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var store = new MaterializationCheckpointStore(context);

        var result = await store.GetOrCreateAsync("products.full");

        Assert.Equal(CreateCheckpoint("products.full"), result);
        var entity = await context.MaterializationStates.SingleAsync();
        Assert.Equal("products.full", entity.PolicyKey);
    }

    [Fact]
    public async Task GetOrCreateAsync_DoesNotCreateDuplicate_WhenCheckpointExists()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var store = new MaterializationCheckpointStore(context);

        var first = await store.GetOrCreateAsync("products.full");
        var second = await store.GetOrCreateAsync("products.full");

        Assert.Equal(first, second);
        Assert.Equal(1, await context.MaterializationStates.CountAsync());
    }

    [Fact]
    public async Task SaveAsync_UpdatesAllCheckpointFields()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var store = new MaterializationCheckpointStore(context);
        await store.GetOrCreateAsync("products.full");
        var checkpoint = CreateCompleteCheckpoint("products.full");

        await store.SaveAsync(checkpoint);
        context.ChangeTracker.Clear();

        var result = await store.GetOrCreateAsync("products.full");
        Assert.Equal(checkpoint, result);
    }

    [Fact]
    public async Task SaveAsync_CreatesCheckpoint_WhenItDoesNotExist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var store = new MaterializationCheckpointStore(context);
        var checkpoint = CreateCompleteCheckpoint("products.full");

        await store.SaveAsync(checkpoint);
        context.ChangeTracker.Clear();

        var result = await store.GetOrCreateAsync("products.full");
        Assert.Equal(checkpoint, result);
        Assert.Equal(1, await context.MaterializationStates.CountAsync());
    }

    private static ReadModelDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ReadModelDbContext>()
            .UseSqlite(connection)
            .Options;

        return new ReadModelDbContext(options);
    }

    private static MaterializationCheckpoint CreateCheckpoint(string policyKey) => new(
        policyKey,
        null,
        null,
        null,
        null,
        null,
        null);

    private static MaterializationCheckpoint CreateCompleteCheckpoint(string policyKey) => new(
        policyKey,
        250,
        new DateTime(2026, 10, 7, 8, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 10, 7, 8, 1, 0, DateTimeKind.Utc),
        new DateTime(2026, 10, 7, 8, 2, 0, DateTimeKind.Utc),
        new DateTime(2026, 10, 7, 8, 3, 0, DateTimeKind.Utc),
        "Test error");
}
