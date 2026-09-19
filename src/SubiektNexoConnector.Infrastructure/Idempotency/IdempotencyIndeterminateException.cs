namespace SubiektNexoConnector.Infrastructure.Idempotency;

public sealed class IdempotencyIndeterminateException(Exception innerException)
    : Exception("The result of the operation is uncertain. Check the resource list before taking further action.", innerException);
