using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace SubiektNexoConnector.Infrastructure.Idempotency.Persistence;

public sealed class IdempotencyOperationStore(
    IDbContextFactory<IdempotencyDbContext> contextFactory)
{
    public Task<bool> ReleasePendingAsync(long operationId, CancellationToken cancellationToken = default) =>
        ExecuteStorageAsync(nameof(ReleasePendingAsync), async () =>
        {
            ValidateOperationId(operationId);
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            return await db.Operations.Where(item => item.Id == operationId
                && item.State == IdempotencyRequestState.Pending).ExecuteDeleteAsync(cancellationToken) == 1;
        });

    // Only call before accepting traffic, while exclusively owning the adapter instance.
    public Task RecoverInterruptedAsync(CancellationToken cancellationToken = default) =>
        ExecuteStorageAsync(nameof(RecoverInterruptedAsync), async () =>
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;
            await db.Operations.Where(item => item.State == IdempotencyRequestState.Executing)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.State, IdempotencyRequestState.Indeterminate)
                    .SetProperty(item => item.UpdatedAtUtc, now), cancellationToken);
            await db.Operations.Where(item => item.State == IdempotencyRequestState.Pending)
                .ExecuteDeleteAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        });

    public Task<bool> MarkExecutingAsync(long operationId, DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        ExecuteStorageAsync(nameof(MarkExecutingAsync), () => MarkExecutingCoreAsync(operationId, now, cancellationToken));

    public Task<bool> MarkIndeterminateAsync(long operationId, DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        ExecuteStorageAsync(nameof(MarkIndeterminateAsync), () => MarkIndeterminateCoreAsync(operationId, now, cancellationToken));

    public Task<bool> MarkCompletedAsync(long operationId, int responseStatusCode,
        string? responseContentType, string? responseLocation, byte[] responseBody,
        DateTimeOffset completedAtUtc, DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default) =>
        ExecuteStorageAsync(nameof(MarkCompletedAsync), () => MarkCompletedCoreAsync(operationId,
            responseStatusCode, responseContentType, responseLocation, responseBody,
            completedAtUtc, expiresAtUtc, cancellationToken));

    public Task<IdempotencyOperationEntity?> FindAsync(string clientId, string key,
        CancellationToken cancellationToken = default) =>
        ExecuteStorageAsync(nameof(FindAsync), () => FindCoreAsync(clientId, key, cancellationToken));

    public Task<IdempotencyCreateResult> CreateAsync(string clientId, string key, string method,
        string requestTarget, ReadOnlyMemory<byte> body, DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default) =>
        ExecuteStorageAsync(nameof(CreateAsync), () => CreateCoreAsync(clientId, key, method,
            requestTarget, body, createdAtUtc, cancellationToken));

    private static async Task<T> ExecuteStorageAsync<T>(string operationName, Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (SqliteException exception)
        {
            throw new IdempotencyStorageException(operationName,
                exception.SqliteErrorCode is 5 or 6, exception);
        }
        catch (DbUpdateException exception)
        {
            var isBusy = exception.InnerException is SqliteException { SqliteErrorCode: 5 or 6 };
            throw new IdempotencyStorageException(operationName, isBusy, exception);
        }
    }

    // False means that the record is missing or its state no longer matches.
    // Database errors propagate; callers must not execute Nexo after a failed transition.
    private async Task<bool> MarkExecutingCoreAsync(
        long operationId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ValidateOperationId(operationId);
        var updatedAt = now.ToUniversalTime();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var affected = await db.Operations
            .Where(item => item.Id == operationId && item.State == IdempotencyRequestState.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.State, IdempotencyRequestState.Executing)
                .SetProperty(item => item.UpdatedAtUtc, updatedAt), cancellationToken);
        return affected == 1;
    }

    private async Task<bool> MarkIndeterminateCoreAsync(
        long operationId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ValidateOperationId(operationId);
        var updatedAt = now.ToUniversalTime();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var affected = await db.Operations
            .Where(item => item.Id == operationId && item.State == IdempotencyRequestState.Executing)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.State, IdempotencyRequestState.Indeterminate)
                .SetProperty(item => item.UpdatedAtUtc, updatedAt), cancellationToken);
        return affected == 1;
    }

    private async Task<bool> MarkCompletedCoreAsync(
        long operationId,
        int responseStatusCode,
        string? responseContentType,
        string? responseLocation,
        byte[] responseBody,
        DateTimeOffset completedAtUtc,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        ValidateOperationId(operationId);
        if (responseStatusCode is < 100 or > 599)
            throw new ArgumentOutOfRangeException(nameof(responseStatusCode));
        if (expiresAtUtc <= completedAtUtc)
            throw new ArgumentOutOfRangeException(nameof(expiresAtUtc), "Expiration must be later than completion.");
        ArgumentNullException.ThrowIfNull(responseBody);
        if (responseContentType?.Length > IdempotencyOperationEntity.MaxResponseContentTypeLength)
            throw new ArgumentException("Response content type is too long.", nameof(responseContentType));
        if (responseLocation?.Length > IdempotencyOperationEntity.MaxResponseLocationLength)
            throw new ArgumentException("Response location is too long.", nameof(responseLocation));

        var completedAt = completedAtUtc.ToUniversalTime();
        var expiresAt = expiresAtUtc.ToUniversalTime();
        var body = responseBody.ToArray();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var affected = await db.Operations
            .Where(item => item.Id == operationId && item.State == IdempotencyRequestState.Executing)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.State, IdempotencyRequestState.Completed)
                .SetProperty(item => item.ResponseStatusCode, responseStatusCode)
                .SetProperty(item => item.ResponseContentType, responseContentType)
                .SetProperty(item => item.ResponseLocation, responseLocation)
                .SetProperty(item => item.ResponseBody, body)
                .SetProperty(item => item.UpdatedAtUtc, completedAt)
                .SetProperty(item => item.CompletedAtUtc, completedAt)
                .SetProperty(item => item.ExpiresAtUtc, expiresAt), cancellationToken);
        return affected == 1;
    }

    private static void ValidateOperationId(long operationId)
    {
        if (operationId <= 0)
            throw new ArgumentOutOfRangeException(nameof(operationId));
    }

    private async Task<IdempotencyOperationEntity?> FindCoreAsync(
        string clientId,
        string key,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Operations.AsNoTracking().SingleOrDefaultAsync(
            item => item.ClientId == clientId && item.Key == key,
            cancellationToken);
    }

    private async Task<IdempotencyCreateResult> CreateCoreAsync(
        string clientId,
        string key,
        string method,
        string requestTarget,
        ReadOnlyMemory<byte> body,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        var operation = new IdempotencyOperationEntity(
            clientId, key, method, requestTarget,
            IdempotencyRequestHasher.Compute(method, requestTarget, body), createdAtUtc);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "IdempotencyOperations"
                ("ClientId", "Key", "Method", "RequestTarget", "RequestHash", "State", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES
                ({operation.ClientId}, {operation.Key}, {operation.Method}, {operation.RequestTarget},
                 {operation.RequestHash}, {operation.State.ToString()}, {operation.CreatedAtUtc}, {operation.UpdatedAtUtc})
            ON CONFLICT ("ClientId", "Key") DO NOTHING;
            """, cancellationToken);

        // The unique index is the atomic admission ticket. Whether this request inserted
        // it or lost the race, compare against the single canonical database record.
        var persisted = await db.Operations.AsNoTracking().SingleAsync(
            item => item.ClientId == clientId && item.Key == key,
            cancellationToken);

        if (inserted == 1)
            return new(IdempotencyCreateOutcome.Created, persisted);

        var matches = persisted.Method == operation.Method
            && persisted.RequestTarget == operation.RequestTarget
            && persisted.RequestHash == operation.RequestHash;
        return new(matches ? IdempotencyCreateOutcome.Existing : IdempotencyCreateOutcome.Conflict,
            persisted);
    }
}
