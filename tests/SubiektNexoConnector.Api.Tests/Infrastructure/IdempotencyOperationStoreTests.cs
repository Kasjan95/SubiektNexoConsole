using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SubiektNexoConnector.Infrastructure.Idempotency;
using SubiektNexoConnector.Infrastructure.Idempotency.Persistence;

namespace SubiektNexoConnector.Api.Tests.Infrastructure;

public sealed class IdempotencyOperationStoreTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "idempotency-store-" + Guid.NewGuid().ToString("N"));
    private ServiceProvider _services = null!;
    private IdempotencyOperationStore Store => _services.GetRequiredService<IdempotencyOperationStore>();

    public async Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Idempotency:DatabasePath"] = Path.Combine(_directory, "test.db")
            }).Build();
        _services = new ServiceCollection().AddIdempotencyPersistence(configuration).BuildServiceProvider();
        await using var db = await _services.GetRequiredService<IDbContextFactory<IdempotencyDbContext>>()
            .CreateDbContextAsync();
        await db.Database.MigrateAsync();
    }

    [Fact]
    public async Task NewRequest_IsCreatedAndCanBeFound()
    {
        var result = await CreateAsync();
        Assert.Equal(IdempotencyCreateOutcome.Created, result.Outcome);
        Assert.Equal(IdempotencyRequestState.Pending, result.Operation.State);
        Assert.Equal(64, result.Operation.RequestHash.Length);
        var persisted = await Store.FindAsync("client-a", "key-1");
        Assert.NotNull(persisted);
        Assert.Equal(result.Operation.Id, persisted.Id);
        Assert.Null(await Store.FindAsync("client-a", "missing"));
    }

    [Fact]
    public async Task IdenticalRequest_ReturnsExistingOperation()
    {
        var first = await CreateAsync();
        var second = await CreateAsync(method: "post");
        Assert.Equal(IdempotencyCreateOutcome.Existing, second.Outcome);
        Assert.Equal(first.Operation.Id, second.Operation.Id);
        Assert.Equal(1, await CountAsync());
    }

    [Theory]
    [InlineData("POST", "/products", "{\"sku\":\"XYZ\"}")]
    [InlineData("PATCH", "/products", "{\"sku\":\"ABC\"}")]
    [InlineData("POST", "/parties", "{\"sku\":\"ABC\"}")]
    [InlineData("POST", "/products", "{ \"sku\": \"ABC\" }")]
    public async Task SameKeyWithDifferentRequest_ReturnsConflictWithoutInserting(
        string method, string target, string body)
    {
        var first = await CreateAsync();
        var result = await CreateAsync(method: method, target: target, body: body);
        Assert.Equal(IdempotencyCreateOutcome.Conflict, result.Outcome);
        Assert.Equal(first.Operation.Id, result.Operation.Id);
        Assert.Equal(first.Operation.RequestHash, result.Operation.RequestHash);
        Assert.Equal(1, await CountAsync());
    }

    [Fact]
    public async Task DifferentClientOrKey_CreatesSeparateOperations()
    {
        await CreateAsync();
        Assert.Equal(IdempotencyCreateOutcome.Created, (await CreateAsync(client: "client-b")).Outcome);
        Assert.Equal(IdempotencyCreateOutcome.Created, (await CreateAsync(key: "key-2")).Outcome);
        Assert.Equal(3, await CountAsync());
    }

    [Fact]
    public async Task ConcurrentIdenticalRequests_CreateExactlyOneOperation()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => CreateAsync())));
        Assert.Single(results, item => item.Outcome == IdempotencyCreateOutcome.Created);
        Assert.Equal(7, results.Count(item => item.Outcome == IdempotencyCreateOutcome.Existing));
        Assert.Single(results.Select(item => item.Operation.Id).Distinct());
        Assert.Equal(1, await CountAsync());
    }

    [Fact]
    public async Task OtherDatabaseErrors_AreNotReportedAsDuplicates()
    {
        await using var db = await _services.GetRequiredService<IDbContextFactory<IdempotencyDbContext>>()
            .CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync("DROP TABLE IdempotencyOperations");
        var exception = await Assert.ThrowsAsync<IdempotencyStorageException>(() => CreateAsync());
        Assert.False(exception.IsBusy);
        Assert.IsType<Microsoft.Data.Sqlite.SqliteException>(exception.InnerException);
    }

    [Fact]
    public async Task Completion_PersistsResponseAndPreventsFurtherTransitions()
    {
        var operation = (await CreateAsync()).Operation;
        var now = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(2));
        Assert.False(await CompleteAsync(operation.Id, now));
        Assert.False(await Store.MarkIndeterminateAsync(operation.Id, now));
        Assert.True(await Store.MarkExecutingAsync(operation.Id, now));
        Assert.False(await Store.MarkExecutingAsync(operation.Id, now));
        Assert.True(await CompleteAsync(operation.Id, now.AddSeconds(1)));
        Assert.False(await CompleteAsync(operation.Id, now.AddSeconds(2)));
        Assert.False(await Store.MarkIndeterminateAsync(operation.Id, now));
        Assert.False(await Store.MarkExecutingAsync(operation.Id, now));

        var persisted = (await Store.FindAsync("client-a", "key-1"))!;
        Assert.Equal(IdempotencyRequestState.Completed, persisted.State);
        Assert.Equal(201, persisted.ResponseStatusCode);
        Assert.Equal("application/json", persisted.ResponseContentType);
        Assert.Equal("/products/ABC", persisted.ResponseLocation);
        Assert.Equal("{\"sku\":\"ABC\"}"u8.ToArray(), persisted.ResponseBody);
        Assert.Equal(now.AddSeconds(1).ToUniversalTime(), persisted.CompletedAtUtc);
        Assert.Equal(persisted.CompletedAtUtc, persisted.UpdatedAtUtc);
        Assert.Equal(now.AddSeconds(1).AddHours(72).ToUniversalTime(), persisted.ExpiresAtUtc);
        Assert.Equal(TimeSpan.Zero, persisted.UpdatedAtUtc.Offset);
    }

    [Fact]
    public async Task Indeterminate_RequiresExecutingAndIsTerminal()
    {
        var id = (await CreateAsync()).Operation.Id;
        var now = DateTimeOffset.UtcNow;
        Assert.False(await Store.MarkIndeterminateAsync(id, now));
        Assert.True(await Store.MarkExecutingAsync(id, now));
        Assert.True(await Store.MarkIndeterminateAsync(id, now.AddSeconds(1)));
        Assert.False(await Store.MarkIndeterminateAsync(id, now));
        Assert.False(await Store.MarkExecutingAsync(id, now));
        Assert.False(await CompleteAsync(id, now));
        var persisted = (await Store.FindAsync("client-a", "key-1"))!;
        Assert.Equal(IdempotencyRequestState.Indeterminate, persisted.State);
        Assert.Equal(now.AddSeconds(1), persisted.UpdatedAtUtc);
        Assert.Null(persisted.ResponseBody);
        Assert.Null(persisted.CompletedAtUtc);
    }

    [Fact]
    public async Task ConcurrentExecutionClaims_ExactlyOneSucceeds()
    {
        var id = (await CreateAsync()).Operation.Id;
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            Task.Run(() => Store.MarkExecutingAsync(id, DateTimeOffset.UtcNow))));
        Assert.Equal(1, results.Count(value => value));
    }

    [Fact]
    public async Task ConcurrentTerminalTransitions_CannotOverwriteEachOther()
    {
        var id = (await CreateAsync()).Operation.Id;
        var now = DateTimeOffset.UtcNow;
        Assert.True(await Store.MarkExecutingAsync(id, now));
        var results = await Task.WhenAll(
            Task.Run(() => CompleteAsync(id, now)),
            Task.Run(() => Store.MarkIndeterminateAsync(id, now)));
        Assert.Equal(1, results.Count(value => value));
        var persisted = (await Store.FindAsync("client-a", "key-1"))!;
        Assert.Equal(results[0] ? IdempotencyRequestState.Completed : IdempotencyRequestState.Indeterminate,
            persisted.State);
        Assert.Equal(results[0], persisted.ResponseBody is not null);
    }

    [Fact]
    public async Task MissingOperation_ReturnsFalse()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.False(await Store.MarkExecutingAsync(long.MaxValue, now));
        Assert.False(await Store.MarkIndeterminateAsync(long.MaxValue, now));
        Assert.False(await CompleteAsync(long.MaxValue, now));
    }

    [Fact]
    public async Task InvalidCompletion_DoesNotChangeExecutingRecord()
    {
        var id = (await CreateAsync()).Operation.Id;
        var now = DateTimeOffset.UtcNow;
        Assert.True(await Store.MarkExecutingAsync(id, now));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Store.MarkCompletedAsync(
            id, 600, null, null, [], now, now.AddHours(1)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Store.MarkCompletedAsync(
            id, 201, null, null, [], now, now));
        await Assert.ThrowsAsync<ArgumentNullException>(() => Store.MarkCompletedAsync(
            id, 201, null, null, null!, now, now.AddHours(1)));
        await Assert.ThrowsAsync<ArgumentException>(() => Store.MarkCompletedAsync(
            id, 201, new string('x', 129), null, [], now, now.AddHours(1)));
        await Assert.ThrowsAsync<ArgumentException>(() => Store.MarkCompletedAsync(
            id, 201, null, new string('x', 2049), [], now, now.AddHours(1)));
        var persisted = (await Store.FindAsync("client-a", "key-1"))!;
        Assert.Equal(IdempotencyRequestState.Executing, persisted.State);
        Assert.Null(persisted.ResponseBody);
    }

    [Fact]
    public async Task PersistentWriteLock_IsReportedAsBusyStorageFailure()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(
            new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            { DataSource = Path.Combine(_directory, "test.db"), Pooling = false }.ToString());
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        var exception = await Assert.ThrowsAsync<IdempotencyStorageException>(() => CreateAsync());
        Assert.True(exception.IsBusy);
        Assert.Equal(nameof(IdempotencyOperationStore.CreateAsync), exception.OperationName);
        Assert.IsType<Microsoft.Data.Sqlite.SqliteException>(exception.InnerException);
    }

    [Fact]
    public async Task BriefWriteLock_IsRetriedBySqliteDriver()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(
            new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            { DataSource = Path.Combine(_directory, "test.db"), Pooling = false }.ToString());
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        var release = Task.Run(async () =>
        {
            await Task.Delay(150);
            transaction.Rollback();
        });
        try
        {
            Assert.Equal(IdempotencyCreateOutcome.Created, (await CreateAsync()).Outcome);
        }
        finally
        {
            await release;
        }
    }

    [Fact]
    public async Task Cancellation_IsNotWrappedAsStorageFailure()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Store.FindAsync("client-a", "key-1", cancellation.Token));
    }

    [Fact]
    public async Task StartupRecovery_ReleasesPendingAndMarksExecutingIndeterminate()
    {
        await CreateAsync(key: "pending");
        var executing = (await CreateAsync(key: "executing")).Operation;
        var completed = (await CreateAsync(key: "completed")).Operation;
        var now = DateTimeOffset.UtcNow;
        await Store.MarkExecutingAsync(executing.Id, now);
        await Store.MarkExecutingAsync(completed.Id, now);
        await CompleteAsync(completed.Id, now);
        await Store.RecoverInterruptedAsync();
        Assert.Null(await Store.FindAsync("client-a", "pending"));
        Assert.Equal(IdempotencyRequestState.Indeterminate, (await Store.FindAsync("client-a", "executing"))!.State);
        Assert.Equal(IdempotencyRequestState.Completed, (await Store.FindAsync("client-a", "completed"))!.State);
        Assert.False(await Store.ReleasePendingAsync(completed.Id));
    }

    private Task<bool> CompleteAsync(long id, DateTimeOffset now) => Store.MarkCompletedAsync(
        id, 201, "application/json", "/products/ABC", "{\"sku\":\"ABC\"}"u8.ToArray(), now, now.AddHours(72));

    private Task<IdempotencyCreateResult> CreateAsync(
        string client = "client-a", string key = "key-1", string method = "POST",
        string target = "/products", string body = "{\"sku\":\"ABC\"}") =>
        Store.CreateAsync(client, key, method, target,
            System.Text.Encoding.UTF8.GetBytes(body), DateTimeOffset.UtcNow);

    private async Task<int> CountAsync()
    {
        await using var db = await _services.GetRequiredService<IDbContextFactory<IdempotencyDbContext>>()
            .CreateDbContextAsync();
        return await db.Operations.CountAsync();
    }

    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
