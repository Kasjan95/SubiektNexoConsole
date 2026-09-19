namespace SubiektNexoConnector.Infrastructure.Idempotency;

// Carries request-local metadata into the singleton executor, never a DbContext.
public sealed class IdempotencyExecutionAccessor
{
    private readonly AsyncLocal<IdempotencyExecutionContext?> _current = new();
    public IdempotencyExecutionContext? Current => _current.Value;

    public IDisposable Begin(IdempotencyExecutionContext context)
    {
        var previous = _current.Value;
        _current.Value = context;
        return new Lease(() => _current.Value = previous);
    }

    private sealed class Lease(Action release) : IDisposable
    {
        public void Dispose() => release();
    }
}
