namespace SubiektNexoConnector.Infrastructure.Abstractions;

public sealed class SferaQueueTimeoutException : TimeoutException
{
    public SferaQueueTimeoutException(TimeSpan timeout, int retryAfterSeconds)
        : base($"Timed out after {timeout.TotalSeconds:N0} seconds while waiting for access to Sfera.")
    {
        RetryAfterSeconds = retryAfterSeconds;
    }

    public int RetryAfterSeconds { get; }
}
