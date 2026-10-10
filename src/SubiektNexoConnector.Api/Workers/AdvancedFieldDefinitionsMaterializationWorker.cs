using System.Diagnostics;
using SubiektNexoConnector.Core.Application.AdditionalFields.Materialization;
using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Api.Workers;

public sealed class AdvancedFieldDefinitionsMaterializationWorker(IServiceScopeFactory scopeFactory, ILogger<AdvancedFieldDefinitionsMaterializationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        logger.LogInformation("Advanced field definitions materialization worker started.");

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
            logger.LogInformation("Advanced field definitions materialization worker stopped.");
        }
    }

    internal async Task RunIterationAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var scope = scopeFactory.CreateAsyncScope();
        var refresher = scope.ServiceProvider.GetRequiredService<IAdvancedFieldDefinitionsMaterializationRefresher>();

        foreach (var definition in MaterializationPolicyDefinitions.AdvancedFieldDefinitionsWorkerPolicies.OrderByDescending(definition => definition.Priority).ThenBy(definition => definition.Id, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = MaterializationPolicyDefinitions.GetAdvancedFieldDefinitionsTarget(definition.Id);
            using var policyLogScope = logger.BeginScope(new Dictionary<string, object>
            {
                ["ExecutionOrigin"] = "MaterializationWorker",
                ["MaterializationPolicy"] = definition.Id,
                ["AdditionalFieldTarget"] = target
            });

            try
            {
                var stopwatch = Stopwatch.StartNew();
                var result = await refresher.RefreshIfDueAsync(target, cancellationToken);
                if (!result.Refreshed)
                    continue;

                stopwatch.Stop();
                var fieldCount = result.Definitions.Fields.Count + result.Definitions.Groups.Sum(group => group.Fields.Count);
                logger.LogInformation("Advanced field definitions materialization completed. Target: {Target}; changed: {Changed}; groups: {GroupCount}; fields: {FieldCount}; duration: {DurationMs} ms.", target, result.Changed, result.Definitions.Groups.Count, fieldCount, stopwatch.Elapsed.TotalMilliseconds);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Advanced field definitions materialization failed for target {Target}.", target);
            }
        }
    }
}
