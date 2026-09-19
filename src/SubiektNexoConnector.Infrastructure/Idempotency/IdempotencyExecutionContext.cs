namespace SubiektNexoConnector.Infrastructure.Idempotency;

public sealed class IdempotencyExecutionContext
{
    public long? OperationId { get; private set; }
    public bool ExecutionStarted { get; private set; }

    public void MarkExecutionStarted() => ExecutionStarted = true;

    public void Initialize(long operationId)
    {
        if (operationId <= 0)
            throw new ArgumentOutOfRangeException(nameof(operationId));
        if (OperationId.HasValue)
            throw new InvalidOperationException("An idempotency operation is already assigned to this request.");
        OperationId = operationId;
    }
}
