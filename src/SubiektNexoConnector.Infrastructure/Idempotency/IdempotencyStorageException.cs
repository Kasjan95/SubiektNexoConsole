namespace SubiektNexoConnector.Infrastructure.Idempotency;

public sealed class IdempotencyStorageException(
    string operationName, bool isBusy, Exception innerException)
    : Exception("The idempotency storage operation failed.", innerException)
{
    public string OperationName { get; } = operationName;
    public bool IsBusy { get; } = isBusy;
}
