namespace SubiektNexoConnector.Infrastructure.Idempotency.Persistence;

public enum IdempotencyCreateOutcome
{
    Created,
    Existing,
    Conflict
}

public sealed record IdempotencyCreateResult(
    IdempotencyCreateOutcome Outcome,
    IdempotencyOperationEntity Operation);
