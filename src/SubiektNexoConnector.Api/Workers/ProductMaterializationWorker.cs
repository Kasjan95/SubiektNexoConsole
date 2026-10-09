using System.Diagnostics;
using SubiektNexoConnector.Core.Application.Products;
using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Api.Workers;

public sealed class ProductMaterializationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ProductMaterializationWorker> logger)
    : BackgroundService
{
    private const int MaxErrorLength = 2000;
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        logger.LogInformation("Product materialization worker started.");

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
            logger.LogInformation("Product materialization worker stopped.");
        }
    }

    internal async Task RunIterationAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stopwatch = Stopwatch.StartNew();
        var executedPolicies = 0;
        var failedPolicies = 0;
        var processedProducts = 0;

        logger.LogDebug("Product materialization iteration started.");

        await using var scope = scopeFactory.CreateAsyncScope();
        var refresher = scope.ServiceProvider.GetRequiredService<IProductMaterializationRefresher>();
        var checkpointStore = scope.ServiceProvider.GetRequiredService<IMaterializationCheckpointStore>();

        foreach (var definition in MaterializationPolicyDefinitions.ProductWorkerPolicies
                     .OrderByDescending(definition => definition.Priority)
                     .ThenBy(definition => definition.Id, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var policyLogScope = logger.BeginScope(new Dictionary<string, object>
            {
                ["ExecutionOrigin"] = "MaterializationWorker",
                ["MaterializationPolicy"] = definition.Id
            });

            try
            {
                var result = await RunPolicyAsync(
                    definition,
                    refresher,
                    checkpointStore,
                    cancellationToken);

                if (result is null)
                    continue;

                executedPolicies++;
                processedProducts += result.ProcessedCount;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                failedPolicies++;
                logger.LogError(
                    exception,
                    "Materialization policy {PolicyId} failed.",
                    definition.Id);
            }
        }

        stopwatch.Stop();

        if (processedProducts > 0 || failedPolicies > 0)
        {
            logger.LogInformation(
                "Product materialization iteration completed. Executed policies: {ExecutedPolicies}; failed policies: {FailedPolicies}; processed products: {ProcessedProducts}; duration: {DurationMs} ms.",
                executedPolicies,
                failedPolicies,
                processedProducts,
                stopwatch.Elapsed.TotalMilliseconds);
        }
        else
        {
            logger.LogDebug(
                "Product materialization iteration completed without work. Executed policies: {ExecutedPolicies}; duration: {DurationMs} ms.",
                executedPolicies,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private async Task<ProductMaterializationBatchResult?> RunPolicyAsync(
        MaterializationPolicyDefinition definition,
        IProductMaterializationRefresher refresher,
        IMaterializationCheckpointStore checkpointStore,
        CancellationToken cancellationToken)
    {
        var checkpoint = await checkpointStore.GetOrCreateAsync(
            definition.Id,
            cancellationToken);
        var startedAtUtc = DateTime.UtcNow;

        if (!ShouldRun(definition, checkpoint, startedAtUtc))
            return null;

        if (checkpoint.CycleStartedAtUtc is null)
        {
            checkpoint = checkpoint with
            {
                CycleStartedAtUtc = startedAtUtc
            };

            await checkpointStore.SaveAsync(checkpoint, cancellationToken);
        }

        try
        {
            var result = await refresher.RefreshNextBatchAsync(
                definition,
                cancellationToken);
            var completedAtUtc = DateTime.UtcNow;

            await checkpointStore.SaveAsync(
                checkpoint with
                {
                    NextCursor = result.NextCursor,
                    CycleStartedAtUtc = result.CycleCompleted
                        ? null
                        : checkpoint.CycleStartedAtUtc,
                    LastBatchCompletedAtUtc = completedAtUtc,
                    LastCycleCompletedAtUtc = result.CycleCompleted
                        ? completedAtUtc
                        : checkpoint.LastCycleCompletedAtUtc,
                    LastErrorAtUtc = null,
                    LastError = null
                },
                cancellationToken);

            logger.LogDebug(
                "Materialization policy {PolicyId} processed {ProcessedCount} products. Cycle completed: {CycleCompleted}.",
                definition.Id,
                result.ProcessedCount,
                result.CycleCompleted);

            if (definition.CandidateSelection == MaterializationCandidateSelection.MissingFromReadModel)
            {
                logger.LogInformation(
                    "New product discovery completed. Processed products: {ProcessedCount}.",
                    result.ProcessedCount);
            }

            return result;
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

            await checkpointStore.SaveAsync(
                checkpoint with
                {
                    LastErrorAtUtc = DateTime.UtcNow,
                    LastError = error
                },
                cancellationToken);

            throw;
        }
    }

    private static bool ShouldRun(
        MaterializationPolicyDefinition definition,
        MaterializationCheckpoint checkpoint,
        DateTime utcNow)
    {
        if (checkpoint.CycleStartedAtUtc is not null)
            return true;

        if (definition.CandidateSelection != MaterializationCandidateSelection.MissingFromReadModel)
            return true;

        return checkpoint.LastCycleCompletedAtUtc is null
            || checkpoint.LastCycleCompletedAtUtc.Value + definition.Lifetime <= utcNow;
    }
}
