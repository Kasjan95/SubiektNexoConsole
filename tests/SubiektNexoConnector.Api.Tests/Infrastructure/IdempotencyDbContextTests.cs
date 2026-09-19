using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SubiektNexoConnector.Infrastructure.Idempotency;
using SubiektNexoConnector.Infrastructure.Idempotency.Persistence;

namespace SubiektNexoConnector.Api.Tests.Infrastructure;

public sealed class IdempotencyDbContextTests
{
    [Fact]
    public async Task Schema_RejectsDuplicateKeyForTheSameClient()
    {
        await using var connection = await OpenConnectionAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.UtcNow;

        context.Operations.Add(CreateOperation("backend-a", "operation-1", now));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        context.Operations.Add(CreateOperation("backend-a", "operation-1", now));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Schema_AllowsTheSameKeyForDifferentClients()
    {
        await using var connection = await OpenConnectionAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.UtcNow;

        context.Operations.Add(CreateOperation("backend-a", "operation-1", now));
        context.Operations.Add(CreateOperation("backend-b", "operation-1", now));
        await context.SaveChangesAsync();

        Assert.Equal(2, await context.Operations.CountAsync());
    }

    [Fact]
    public async Task StateAndResponse_ArePersistedForReplay()
    {
        await using var connection = await OpenConnectionAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var createdAt = DateTimeOffset.UtcNow;
        var request = CreateOperation("backend-a", "operation-1", createdAt);

        request.MarkExecuting(createdAt.AddSeconds(1));
        request.MarkCompleted(
            StatusCodes.Status201Created,
            "application/json",
            "/products/ABC",
            "{\"sku\":\"ABC\"}"u8.ToArray(),
            createdAt.AddSeconds(2),
            createdAt.AddHours(72));
        context.Operations.Add(request);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var persisted = await context.Operations.SingleAsync();

        Assert.Equal(IdempotencyRequestState.Completed, persisted.State);
        Assert.Equal(StatusCodes.Status201Created, persisted.ResponseStatusCode);
        Assert.Equal("application/json", persisted.ResponseContentType);
        Assert.Equal("/products/ABC", persisted.ResponseLocation);
        Assert.Equal("{\"sku\":\"ABC\"}", System.Text.Encoding.UTF8.GetString(persisted.ResponseBody!));
        Assert.Equal(createdAt.AddSeconds(2), persisted.CompletedAtUtc);
        Assert.Equal(createdAt.AddHours(72), persisted.ExpiresAtUtc);
    }

    [Fact]
    public void StateMachine_OnlyAllowsIndeterminateAfterExecutionStarted()
    {
        var now = DateTimeOffset.UtcNow;
        var request = CreateOperation("backend-a", "operation-1", now);

        Assert.Throws<InvalidOperationException>(() => request.MarkIndeterminate(now.AddSeconds(1)));

        request.MarkExecuting(now.AddSeconds(2));
        request.MarkIndeterminate(now.AddSeconds(3));

        Assert.Equal(IdempotencyRequestState.Indeterminate, request.State);
    }

    private static IdempotencyOperationEntity CreateOperation(string clientId, string key, DateTimeOffset now) =>
        new(clientId, key, "post", "/products", new string('a', 64), now);

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static IdempotencyDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<IdempotencyDbContext>()
            .UseSqlite(connection)
            .Options;
        return new IdempotencyDbContext(options);
    }
}
