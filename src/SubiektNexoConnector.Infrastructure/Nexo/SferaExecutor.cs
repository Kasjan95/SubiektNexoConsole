using InsERT.Moria.Sfera;
using Microsoft.Extensions.Logging;
using SubiektNexoConnector.Infrastructure.Abstractions;
using SubiektNexoConnector.Infrastructure.Configuration;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using SubiektNexoConnector.Infrastructure.Idempotency;
using SubiektNexoConnector.Infrastructure.Idempotency.Persistence;

namespace SubiektNexoConnector.Infrastructure.Nexo;

public sealed class SferaExecutor : ISferaExecutor, IDisposable
{
    private readonly ISessionFactory _sessionFactory;
    private readonly SferaExecutionOptions _options;
    private readonly ILogger<SferaExecutor> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _pendingOperations;
    private readonly IdempotencyOperationStore? _idempotencyStore;
    private readonly IdempotencyExecutionAccessor? _idempotencyAccessor;

    public SferaExecutor(
        ISessionFactory sessionFactory,
        SferaExecutionOptions options,
        ILogger<SferaExecutor> logger,
        IdempotencyOperationStore? idempotencyStore = null,
        IdempotencyExecutionAccessor? idempotencyAccessor = null)
    {
        _sessionFactory = sessionFactory;
        _options = options;
        _logger = logger;
        _idempotencyStore = idempotencyStore;
        _idempotencyAccessor = idempotencyAccessor;
    }

    public T Execute<T>(
        Func<Uchwyt, T> operation,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string operationName = "",
        [CallerFilePath] string sourceFilePath = "")
    {
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();

        var operationId = $"{Path.GetFileNameWithoutExtension(sourceFilePath)}.{operationName}";
        var queueDepthAtArrival = Interlocked.Increment(ref _pendingOperations) - 1;
        var queueStopwatch = Stopwatch.StartNew();
        var gateEntered = false;
        var success = false;
        var connectionStopwatch = new Stopwatch();
        var operationStopwatch = new Stopwatch();
        var disposeStopwatch = new Stopwatch();
        Uchwyt? sfera = null;

        try
        {
            gateEntered = _gate.Wait(
                TimeSpan.FromSeconds(_options.QueueTimeoutSeconds),
                cancellationToken);
            queueStopwatch.Stop();

            if (!gateEntered)
            {
                _logger.LogWarning(
                    "Sfera operation {SferaOperation} timed out in the queue after {QueueWaitMs} ms. Queue depth at arrival: {QueueDepthAtArrival}.",
                    operationId,
                    queueStopwatch.Elapsed.TotalMilliseconds,
                    queueDepthAtArrival);

                throw new SferaQueueTimeoutException(
                    TimeSpan.FromSeconds(_options.QueueTimeoutSeconds),
                    _options.RetryAfterSeconds);
            }

            cancellationToken.ThrowIfCancellationRequested();

            connectionStopwatch.Start();
            try
            {
                sfera = _sessionFactory.Create();
            }
            finally
            {
                connectionStopwatch.Stop();
            }

            var execution = _idempotencyAccessor?.Current;
            if (execution?.OperationId is long id && !execution.ExecutionStarted)
            {
                if (_idempotencyStore is null || !_idempotencyStore
                    .MarkExecutingAsync(id, DateTimeOffset.UtcNow).GetAwaiter().GetResult())
                    throw new InvalidOperationException("The idempotency operation cannot enter Executing.");
                execution.MarkExecutionStarted();
            }

            operationStopwatch.Start();
            try
            {
                var result = operation(sfera);
                success = true;
                return result;
            }
            finally
            {
                operationStopwatch.Stop();
            }
        }
        finally
        {
            try
            {
                if (sfera is not null)
                {
                    disposeStopwatch.Start();
                    try
                    {
                        sfera.Dispose();
                    }
                    finally
                    {
                        disposeStopwatch.Stop();
                    }
                }
            }
            finally
            {
                if (gateEntered)
                {
                    _gate.Release();
                    var totalSferaMs = connectionStopwatch.Elapsed.TotalMilliseconds
                        + operationStopwatch.Elapsed.TotalMilliseconds
                        + disposeStopwatch.Elapsed.TotalMilliseconds;
                    _logger.LogDebug(
                        "Sfera operation {SferaOperation} completed. Queue wait: {QueueWaitMs} ms. Connection: {SferaConnectionMs} ms. Operation: {SferaOperationMs} ms. Dispose: {SferaDisposeMs} ms. Total: {SferaTotalMs} ms. Queue depth at arrival: {QueueDepthAtArrival}. Success: {Success}.",
                        operationId,
                        queueStopwatch.Elapsed.TotalMilliseconds,
                        connectionStopwatch.Elapsed.TotalMilliseconds,
                        operationStopwatch.Elapsed.TotalMilliseconds,
                        disposeStopwatch.Elapsed.TotalMilliseconds,
                        totalSferaMs,
                        queueDepthAtArrival,
                        success);
                }

                Interlocked.Decrement(ref _pendingOperations);
            }
        }
    }

    public void Execute(
        Action<Uchwyt> operation,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string operationName = "",
        [CallerFilePath] string sourceFilePath = "")
    {
        ArgumentNullException.ThrowIfNull(operation);
        Execute(sfera =>
        {
            operation(sfera);
            return true;
        }, cancellationToken, operationName, sourceFilePath);
    }

    public void Dispose()
    {
        _gate.Dispose();
    }
}
