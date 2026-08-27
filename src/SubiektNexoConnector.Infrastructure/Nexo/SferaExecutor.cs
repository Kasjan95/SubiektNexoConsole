using InsERT.Moria.Sfera;
using Microsoft.Extensions.Logging;
using SubiektNexoConnector.Infrastructure.Abstractions;
using SubiektNexoConnector.Infrastructure.Configuration;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SubiektNexoConnector.Infrastructure.Nexo;

public sealed class SferaExecutor : ISferaExecutor, IDisposable
{
    private readonly ISessionFactory _sessionFactory;
    private readonly SferaExecutionOptions _options;
    private readonly ILogger<SferaExecutor> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _pendingOperations;

    public SferaExecutor(
        ISessionFactory sessionFactory,
        SferaExecutionOptions options,
        ILogger<SferaExecutor> logger)
    {
        _sessionFactory = sessionFactory;
        _options = options;
        _logger = logger;
    }

    public T Execute<T>(
        Func<Uchwyt, T> operation,
        [CallerMemberName] string operationName = "",
        [CallerFilePath] string sourceFilePath = "")
    {
        ArgumentNullException.ThrowIfNull(operation);

        var operationId = $"{Path.GetFileNameWithoutExtension(sourceFilePath)}.{operationName}";
        var queueDepthAtArrival = Interlocked.Increment(ref _pendingOperations) - 1;
        var queueStopwatch = Stopwatch.StartNew();
        var gateEntered = false;
        var success = false;
        var executionStopwatch = new Stopwatch();

        try
        {
            gateEntered = _gate.Wait(TimeSpan.FromSeconds(_options.QueueTimeoutSeconds));
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

            executionStopwatch.Start();
            using var sfera = _sessionFactory.Create();
            var result = operation(sfera);
            success = true;
            return result;
        }
        finally
        {
            executionStopwatch.Stop();

            if (gateEntered)
            {
                _gate.Release();
                _logger.LogInformation(
                    "Sfera operation {SferaOperation} completed. Queue wait: {QueueWaitMs} ms. Execution: {SferaExecutionMs} ms. Queue depth at arrival: {QueueDepthAtArrival}. Success: {Success}.",
                    operationId,
                    queueStopwatch.Elapsed.TotalMilliseconds,
                    executionStopwatch.Elapsed.TotalMilliseconds,
                    queueDepthAtArrival,
                    success);
            }

            Interlocked.Decrement(ref _pendingOperations);
        }
    }

    public void Execute(
        Action<Uchwyt> operation,
        [CallerMemberName] string operationName = "",
        [CallerFilePath] string sourceFilePath = "")
    {
        ArgumentNullException.ThrowIfNull(operation);
        Execute(sfera =>
        {
            operation(sfera);
            return true;
        }, operationName, sourceFilePath);
    }

    public void Dispose()
    {
        _gate.Dispose();
    }
}
