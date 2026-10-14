using System.Diagnostics;
using SubiektNexoConnector.Core.Application.Parties;
using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Api.Workers;

public sealed class PartyMaterializationWorker(IServiceScopeFactory scopeFactory, ILogger<PartyMaterializationWorker> logger) : BackgroundService
{
    private const int MaxErrorLength = 2000;
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        logger.LogInformation("Party materialization worker started.");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await RunIterationAsync(stoppingToken);
                await Task.Delay(IdleDelay, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Party materialization worker stopped.");
        }
    }

    internal async Task RunIterationAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var scope = scopeFactory.CreateAsyncScope();
        var refresher = scope.ServiceProvider.GetRequiredService<IPartyMaterializationRefresher>();
        var checkpointStore = scope.ServiceProvider.GetRequiredService<IMaterializationCheckpointStore>();
        var policyStore = scope.ServiceProvider.GetRequiredService<IMaterializationPolicyStore>();
        var definition = MaterializationPolicyDefinitions.Parties;
        var policy = await policyStore.GetOrCreateAsync(definition, cancellationToken);
        var checkpoint = await checkpointStore.GetOrCreateAsync(definition.Id, cancellationToken);
        var utcNow = DateTime.UtcNow;

        if (!ShouldRun(checkpoint, policy, utcNow))
            return;

        using var policyLogScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["ExecutionOrigin"] = "MaterializationWorker",
            ["MaterializationPolicy"] = definition.Id
        });

        if (checkpoint.CycleStartedAtUtc is null)
        {
            checkpoint = checkpoint with { CycleStartedAtUtc = utcNow, NextCursor = null };
            await checkpointStore.SaveAsync(checkpoint, cancellationToken);
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await refresher.RefreshNextBatchAsync(checkpoint.NextCursor, policy.BatchSize, cancellationToken);
            var removedParties = result.CycleCompleted
                ? await refresher.CompleteCycleAsync(checkpoint.CycleStartedAtUtc!.Value, cancellationToken)
                : 0;
            var completedAtUtc = DateTime.UtcNow;
            await checkpointStore.SaveAsync(checkpoint with
            {
                NextCursor = result.CycleCompleted ? null : result.NextCursor,
                CycleStartedAtUtc = result.CycleCompleted ? null : checkpoint.CycleStartedAtUtc,
                LastBatchCompletedAtUtc = completedAtUtc,
                LastCycleCompletedAtUtc = result.CycleCompleted ? completedAtUtc : checkpoint.LastCycleCompletedAtUtc,
                LastErrorAtUtc = null,
                LastError = null
            }, cancellationToken);

            stopwatch.Stop();
            logger.LogInformation("Party materialization batch completed. Processed parties: {ProcessedParties}; removed parties: {RemovedParties}; cycle completed: {CycleCompleted}; duration: {DurationMs} ms.", result.ProcessedCount, removedParties, result.CycleCompleted, stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var error = exception.ToString();
            if (error.Length > MaxErrorLength)
                error = error[..MaxErrorLength];

            await checkpointStore.SaveAsync(checkpoint with { LastErrorAtUtc = DateTime.UtcNow, LastError = error }, cancellationToken);
            logger.LogError(exception, "Party materialization batch failed.");
        }
    }

    private static bool ShouldRun(MaterializationCheckpoint checkpoint, MaterializationPolicy policy, DateTime utcNow)
    {
        if (checkpoint.CycleStartedAtUtc is not null)
            return true;
        if (checkpoint.LastCycleCompletedAtUtc is null)
            return true;

        return checkpoint.LastCycleCompletedAtUtc.Value + policy.Lifetime - policy.RefreshAhead <= utcNow;
    }
}
