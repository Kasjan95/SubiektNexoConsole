using System.Reflection;
using System.Runtime.ExceptionServices;
using SubiektNexoConnector.Api.Idempotency;
using SubiektNexoConnector.Infrastructure.Idempotency;
using SubiektNexoConnector.Infrastructure.Idempotency.Persistence;

namespace SubiektNexoConnector.Api.Tests.Integration;

public sealed class TestIdempotencyHeaderHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.Headers.Contains(IdempotencyMiddleware.HeaderName))
            request.Headers.Add(IdempotencyMiddleware.HeaderName, Guid.NewGuid().ToString("N"));
        return base.SendAsync(request, cancellationToken);
    }
}

// HTTP tests replace Sfera repositories, so simulate only the missing execution boundary.
public class TestIdempotencyRepositoryProxy<T> : DispatchProxy where T : class
{
    private T _repository = null!;
    private IdempotencyExecutionAccessor _accessor = null!;
    private IdempotencyOperationStore _store = null!;

    public static T Wrap(T repository, IdempotencyExecutionAccessor accessor, IdempotencyOperationStore store)
    {
        var proxy = Create<T, TestIdempotencyRepositoryProxy<T>>();
        var implementation = (TestIdempotencyRepositoryProxy<T>)(object)proxy;
        implementation._repository = repository;
        implementation._accessor = accessor;
        implementation._store = store;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var execution = _accessor.Current;
        if (execution?.OperationId is long id && !execution.ExecutionStarted)
        {
            if (!_store.MarkExecutingAsync(id, DateTimeOffset.UtcNow).GetAwaiter().GetResult())
                throw new InvalidOperationException("Test operation could not enter Executing.");
            execution.MarkExecutionStarted();
        }
        try
        {
            return targetMethod!.Invoke(_repository, args);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}
