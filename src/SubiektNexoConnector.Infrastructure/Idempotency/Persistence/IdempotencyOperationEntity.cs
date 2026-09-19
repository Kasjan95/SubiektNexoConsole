namespace SubiektNexoConnector.Infrastructure.Idempotency.Persistence;

public sealed class IdempotencyOperationEntity
{
    public const int MaxClientIdLength = 128;
    public const int MaxKeyLength = 64;
    public const int MaxMethodLength = 16;
    public const int MaxRequestTargetLength = 2048;
    public const int RequestHashLength = 64;
    public const int MaxResponseContentTypeLength = 128;
    public const int MaxResponseLocationLength = 2048;

    private IdempotencyOperationEntity()
    {
    }

    public IdempotencyOperationEntity(
        string clientId,
        string key,
        string method,
        string requestTarget,
        string requestHash,
        DateTimeOffset createdAtUtc)
    {
        ClientId = RequireValue(clientId, nameof(clientId), MaxClientIdLength);
        Key = RequireValue(key, nameof(key), MaxKeyLength);
        Method = RequireValue(method, nameof(method), MaxMethodLength).ToUpperInvariant();
        RequestTarget = RequireValue(requestTarget, nameof(requestTarget), MaxRequestTargetLength);
        RequestHash = RequireValue(requestHash, nameof(requestHash), RequestHashLength);
        if (RequestHash.Length != RequestHashLength)
            throw new ArgumentException($"Request hash must contain exactly {RequestHashLength} characters.", nameof(requestHash));
        State = IdempotencyRequestState.Pending;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        UpdatedAtUtc = CreatedAtUtc;
    }

    public long Id { get; private set; }
    public string ClientId { get; private set; } = null!;
    public string Key { get; private set; } = null!;
    public string Method { get; private set; } = null!;
    public string RequestTarget { get; private set; } = null!;
    public string RequestHash { get; private set; } = null!;
    public IdempotencyRequestState State { get; private set; }
    public int? ResponseStatusCode { get; private set; }
    public string? ResponseContentType { get; private set; }
    public string? ResponseLocation { get; private set; }
    public byte[]? ResponseBody { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public DateTimeOffset? ExpiresAtUtc { get; private set; }

    public void MarkExecuting(DateTimeOffset now)
    {
        RequireState(IdempotencyRequestState.Pending);
        State = IdempotencyRequestState.Executing;
        UpdatedAtUtc = now.ToUniversalTime();
    }

    public void MarkCompleted(
        int responseStatusCode,
        string? responseContentType,
        string? responseLocation,
        byte[] responseBody,
        DateTimeOffset completedAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        RequireState(IdempotencyRequestState.Executing);
        if (responseStatusCode is < 100 or > 599)
            throw new ArgumentOutOfRangeException(nameof(responseStatusCode));
        if (expiresAtUtc <= completedAtUtc)
            throw new ArgumentOutOfRangeException(nameof(expiresAtUtc), "Expiration must be later than completion.");

        ArgumentNullException.ThrowIfNull(responseBody);
        if (responseContentType?.Length > MaxResponseContentTypeLength)
            throw new ArgumentException($"Response content type cannot exceed {MaxResponseContentTypeLength} characters.", nameof(responseContentType));
        if (responseLocation?.Length > MaxResponseLocationLength)
            throw new ArgumentException($"Response location cannot exceed {MaxResponseLocationLength} characters.", nameof(responseLocation));

        State = IdempotencyRequestState.Completed;
        ResponseStatusCode = responseStatusCode;
        ResponseContentType = responseContentType;
        ResponseLocation = responseLocation;
        ResponseBody = responseBody.ToArray();
        UpdatedAtUtc = completedAtUtc.ToUniversalTime();
        CompletedAtUtc = UpdatedAtUtc;
        ExpiresAtUtc = expiresAtUtc.ToUniversalTime();
    }

    public void MarkIndeterminate(DateTimeOffset now)
    {
        RequireState(IdempotencyRequestState.Executing);
        State = IdempotencyRequestState.Indeterminate;
        UpdatedAtUtc = now.ToUniversalTime();
    }

    private void RequireState(IdempotencyRequestState expected)
    {
        if (State != expected)
            throw new InvalidOperationException($"Expected idempotency state '{expected}', but found '{State}'.");
    }

    private static string RequireValue(string value, string parameterName, int maximumLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > maximumLength)
            throw new ArgumentException($"Value cannot exceed {maximumLength} characters.", parameterName);
        return value;
    }
}
