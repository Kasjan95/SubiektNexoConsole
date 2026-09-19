namespace SubiektNexoConnector.Infrastructure.Idempotency;

public enum IdempotencyRequestState
{
    Pending,
    Executing,
    Completed,
    Indeterminate
}
