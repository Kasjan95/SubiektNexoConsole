using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SubiektNexoConnector.Api.Workers;
using SubiektNexoConnector.Core.Application.Products;
using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Api.Tests.Workers;

public sealed class ProductMaterializationWorkerTests
{
    [Fact]
    public async Task RunIterationAsync_ExecutesPoliciesByDescendingPriority_AndCompletesCheckpoints()
    {
        var refresher = Substitute.For<IProductMaterializationRefresher>();
        var checkpointStore = Substitute.For<IMaterializationCheckpointStore>();
        var executionOrder = new List<string>();
        var savedCheckpoints = new List<MaterializationCheckpoint>();
        ConfigureNewCheckpoints(checkpointStore);
        checkpointStore.SaveAsync(
                Arg.Do<MaterializationCheckpoint>(savedCheckpoints.Add),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        refresher.RefreshNextBatchAsync(
                Arg.Any<MaterializationPolicyDefinition>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var definition = callInfo.Arg<MaterializationPolicyDefinition>();
                executionOrder.Add(definition.Id);
                return Task.FromResult(new ProductMaterializationBatchResult(1, null, true));
            });
        using var services = CreateServices(refresher, checkpointStore);
        var worker = CreateWorker(services);

        await worker.RunIterationAsync(CancellationToken.None);

        Assert.Equal(
            [
                MaterializationPolicyDefinitions.ProductsNew.Id,
                MaterializationPolicyDefinitions.ProductsHot.Id,
                MaterializationPolicyDefinitions.ProductsCold.Id
            ],
            executionOrder);
        Assert.All(MaterializationPolicyDefinitions.ProductWorkerPolicies, definition =>
            Assert.Contains(savedCheckpoints, checkpoint =>
                checkpoint.PolicyKey == definition.Id
                && checkpoint.CycleStartedAtUtc is null
                && checkpoint.LastBatchCompletedAtUtc is not null
                && checkpoint.LastCycleCompletedAtUtc is not null
                && checkpoint.LastError is null));
    }

    [Fact]
    public async Task RunIterationAsync_RecordsFailure_AndContinuesWithRemainingPolicies()
    {
        var refresher = Substitute.For<IProductMaterializationRefresher>();
        var checkpointStore = Substitute.For<IMaterializationCheckpointStore>();
        var executionOrder = new List<string>();
        var savedCheckpoints = new List<MaterializationCheckpoint>();
        ConfigureNewCheckpoints(checkpointStore);
        checkpointStore.SaveAsync(
                Arg.Do<MaterializationCheckpoint>(savedCheckpoints.Add),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        refresher.RefreshNextBatchAsync(
                Arg.Any<MaterializationPolicyDefinition>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var definition = callInfo.Arg<MaterializationPolicyDefinition>();
                executionOrder.Add(definition.Id);

                if (definition.Id == MaterializationPolicyDefinitions.ProductsNew.Id)
                    throw new InvalidOperationException("New product discovery failed.");

                return Task.FromResult(new ProductMaterializationBatchResult(0, null, true));
            });
        using var services = CreateServices(refresher, checkpointStore);
        var worker = CreateWorker(services);

        await worker.RunIterationAsync(CancellationToken.None);

        Assert.Equal(
            [
                MaterializationPolicyDefinitions.ProductsNew.Id,
                MaterializationPolicyDefinitions.ProductsHot.Id,
                MaterializationPolicyDefinitions.ProductsCold.Id
            ],
            executionOrder);
        Assert.Contains(savedCheckpoints, checkpoint =>
            checkpoint.PolicyKey == MaterializationPolicyDefinitions.ProductsNew.Id
            && checkpoint.LastErrorAtUtc is not null
            && checkpoint.LastError?.Contains("New product discovery failed.", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task RunIterationAsync_SkipsRecentNewProductDiscovery()
    {
        var refresher = Substitute.For<IProductMaterializationRefresher>();
        var checkpointStore = Substitute.For<IMaterializationCheckpointStore>();
        var executionOrder = new List<string>();
        checkpointStore.GetOrCreateAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var policyId = callInfo.Arg<string>();
                var lastCycleCompletedAtUtc = policyId == MaterializationPolicyDefinitions.ProductsNew.Id
                    ? DateTime.UtcNow
                    : (DateTime?)null;

                return Task.FromResult(CreateCheckpoint(
                    policyId,
                    lastCycleCompletedAtUtc: lastCycleCompletedAtUtc));
            });
        refresher.RefreshNextBatchAsync(
                Arg.Any<MaterializationPolicyDefinition>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                executionOrder.Add(callInfo.Arg<MaterializationPolicyDefinition>().Id);
                return Task.FromResult(new ProductMaterializationBatchResult(0, null, true));
            });
        using var services = CreateServices(refresher, checkpointStore);
        var worker = CreateWorker(services);

        await worker.RunIterationAsync(CancellationToken.None);

        Assert.Equal(
            [
                MaterializationPolicyDefinitions.ProductsHot.Id,
                MaterializationPolicyDefinitions.ProductsCold.Id
            ],
            executionOrder);
    }

    [Fact]
    public async Task RunIterationAsync_DoesNotStartWork_WhenCancellationWasRequested()
    {
        var refresher = Substitute.For<IProductMaterializationRefresher>();
        var checkpointStore = Substitute.For<IMaterializationCheckpointStore>();
        using var services = CreateServices(refresher, checkpointStore);
        var worker = CreateWorker(services);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            worker.RunIterationAsync(cancellation.Token));

        await checkpointStore.DidNotReceiveWithAnyArgs()
            .GetOrCreateAsync(default!, default);
        await refresher.DidNotReceiveWithAnyArgs()
            .RefreshNextBatchAsync(default!, default);
    }

    private static void ConfigureNewCheckpoints(
        IMaterializationCheckpointStore checkpointStore)
    {
        checkpointStore.GetOrCreateAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(
                CreateCheckpoint(callInfo.Arg<string>())));
    }

    private static ServiceProvider CreateServices(
        IProductMaterializationRefresher refresher,
        IMaterializationCheckpointStore checkpointStore)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => refresher);
        services.AddScoped(_ => checkpointStore);
        return services.BuildServiceProvider();
    }

    private static ProductMaterializationWorker CreateWorker(
        ServiceProvider services) => new(
        services.GetRequiredService<IServiceScopeFactory>(),
        Substitute.For<ILogger<ProductMaterializationWorker>>());

    private static MaterializationCheckpoint CreateCheckpoint(
        string policyId,
        DateTime? cycleStartedAtUtc = null,
        DateTime? lastCycleCompletedAtUtc = null) => new(
        policyId,
        null,
        cycleStartedAtUtc,
        null,
        lastCycleCompletedAtUtc,
        null,
        null);
}
