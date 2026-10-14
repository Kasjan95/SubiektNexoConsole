using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SubiektNexoConnector.Api.Workers;
using SubiektNexoConnector.Core.Application.Parties;
using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Api.Tests.Workers;

public sealed class PartyMaterializationWorkerTests
{
    [Fact]
    public async Task RunIterationAsync_PersistsCursor_WhenCycleIsIncomplete()
    {
        var refresher = Substitute.For<IPartyMaterializationRefresher>();
        var checkpointStore = Substitute.For<IMaterializationCheckpointStore>();
        var policyStore = Substitute.For<IMaterializationPolicyStore>();
        var savedCheckpoints = new List<MaterializationCheckpoint>();
        ConfigurePolicy(policyStore);
        checkpointStore.GetOrCreateAsync(MaterializationPolicyDefinitions.Parties.Id, Arg.Any<CancellationToken>()).Returns(CreateCheckpoint());
        checkpointStore.SaveAsync(Arg.Do<MaterializationCheckpoint>(savedCheckpoints.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        refresher.RefreshNextBatchAsync(null, MaterializationPolicyDefinitions.Parties.BatchSize, Arg.Any<CancellationToken>()).Returns(new PartyMaterializationBatchResult(100, 125, false));
        using var services = CreateServices(refresher, checkpointStore, policyStore);
        var worker = CreateWorker(services);

        await worker.RunIterationAsync(CancellationToken.None);

        await refresher.Received(1).RefreshNextBatchAsync(null, MaterializationPolicyDefinitions.Parties.BatchSize, Arg.Any<CancellationToken>());
        await refresher.DidNotReceiveWithAnyArgs().CompleteCycleAsync(default, default);
        Assert.Contains(savedCheckpoints, checkpoint => checkpoint.NextCursor == 125 && checkpoint.CycleStartedAtUtc is not null && checkpoint.LastCycleCompletedAtUtc is null && checkpoint.LastError is null);
    }

    [Fact]
    public async Task RunIterationAsync_ClearsCursor_AndCompletesCycle()
    {
        var refresher = Substitute.For<IPartyMaterializationRefresher>();
        var checkpointStore = Substitute.For<IMaterializationCheckpointStore>();
        var policyStore = Substitute.For<IMaterializationPolicyStore>();
        var savedCheckpoints = new List<MaterializationCheckpoint>();
        var cycleStartedAtUtc = DateTime.UtcNow.AddMinutes(-1);
        ConfigurePolicy(policyStore);
        checkpointStore.GetOrCreateAsync(MaterializationPolicyDefinitions.Parties.Id, Arg.Any<CancellationToken>()).Returns(CreateCheckpoint(nextCursor: 125, cycleStartedAtUtc: cycleStartedAtUtc));
        checkpointStore.SaveAsync(Arg.Do<MaterializationCheckpoint>(savedCheckpoints.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        refresher.RefreshNextBatchAsync(125, MaterializationPolicyDefinitions.Parties.BatchSize, Arg.Any<CancellationToken>()).Returns(new PartyMaterializationBatchResult(12, null, true));
        refresher.CompleteCycleAsync(cycleStartedAtUtc, Arg.Any<CancellationToken>()).Returns(3);
        using var services = CreateServices(refresher, checkpointStore, policyStore);
        var worker = CreateWorker(services);

        await worker.RunIterationAsync(CancellationToken.None);

        await refresher.Received(1).CompleteCycleAsync(cycleStartedAtUtc, Arg.Any<CancellationToken>());
        Assert.Contains(savedCheckpoints, checkpoint => checkpoint.NextCursor is null && checkpoint.CycleStartedAtUtc is null && checkpoint.LastCycleCompletedAtUtc is not null && checkpoint.LastBatchCompletedAtUtc is not null && checkpoint.LastError is null);
    }

    [Fact]
    public async Task RunIterationAsync_SkipsPolicy_WhenRefreshIsNotDue()
    {
        var refresher = Substitute.For<IPartyMaterializationRefresher>();
        var checkpointStore = Substitute.For<IMaterializationCheckpointStore>();
        var policyStore = Substitute.For<IMaterializationPolicyStore>();
        ConfigurePolicy(policyStore);
        checkpointStore.GetOrCreateAsync(MaterializationPolicyDefinitions.Parties.Id, Arg.Any<CancellationToken>()).Returns(CreateCheckpoint(lastCycleCompletedAtUtc: DateTime.UtcNow));
        using var services = CreateServices(refresher, checkpointStore, policyStore);
        var worker = CreateWorker(services);

        await worker.RunIterationAsync(CancellationToken.None);

        await refresher.DidNotReceiveWithAnyArgs().RefreshNextBatchAsync(default, default, default);
        await checkpointStore.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    [Fact]
    public async Task RunIterationAsync_DoesNotStartWork_WhenCancellationWasRequested()
    {
        var refresher = Substitute.For<IPartyMaterializationRefresher>();
        var checkpointStore = Substitute.For<IMaterializationCheckpointStore>();
        var policyStore = Substitute.For<IMaterializationPolicyStore>();
        using var services = CreateServices(refresher, checkpointStore, policyStore);
        var worker = CreateWorker(services);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => worker.RunIterationAsync(cancellation.Token));

        await policyStore.DidNotReceiveWithAnyArgs().GetOrCreateAsync(default!, default);
        await checkpointStore.DidNotReceiveWithAnyArgs().GetOrCreateAsync(default!, default);
        await refresher.DidNotReceiveWithAnyArgs().RefreshNextBatchAsync(default, default, default);
    }

    private static void ConfigurePolicy(IMaterializationPolicyStore policyStore)
    {
        var definition = MaterializationPolicyDefinitions.Parties;
        policyStore.GetOrCreateAsync(definition, Arg.Any<CancellationToken>()).Returns(new MaterializationPolicy(definition.Id, definition.Name, definition.Lifetime, definition.RefreshAhead, definition.BatchSize, definition.CandidateSelection, definition.Requirements, DateTime.UtcNow));
    }

    private static ServiceProvider CreateServices(IPartyMaterializationRefresher refresher, IMaterializationCheckpointStore checkpointStore, IMaterializationPolicyStore policyStore)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => refresher);
        services.AddScoped(_ => checkpointStore);
        services.AddScoped(_ => policyStore);
        return services.BuildServiceProvider();
    }

    private static PartyMaterializationWorker CreateWorker(ServiceProvider services) => new(services.GetRequiredService<IServiceScopeFactory>(), Substitute.For<ILogger<PartyMaterializationWorker>>());

    private static MaterializationCheckpoint CreateCheckpoint(int? nextCursor = null, DateTime? cycleStartedAtUtc = null, DateTime? lastCycleCompletedAtUtc = null) => new(MaterializationPolicyDefinitions.Parties.Id, nextCursor, cycleStartedAtUtc, null, lastCycleCompletedAtUtc, null, null);
}
