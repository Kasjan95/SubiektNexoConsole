using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SubiektNexoConnector.Api.Workers;
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.Materialization;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;

namespace SubiektNexoConnector.Api.Tests.Workers;

public sealed class AdvancedFieldDefinitionsMaterializationWorkerTests
{
    [Fact]
    public async Task RunIterationAsync_ChecksProductAndPartyDefinitionsInPriorityOrder()
    {
        var refresher = Substitute.For<IAdvancedFieldDefinitionsMaterializationRefresher>();
        var targets = new List<AdditionalFieldTarget>();
        refresher.RefreshIfDueAsync(Arg.Any<AdditionalFieldTarget>(), Arg.Any<CancellationToken>()).Returns(callInfo =>
        {
            var target = callInfo.Arg<AdditionalFieldTarget>();
            targets.Add(target);
            return Task.FromResult(CreateResult(target, refreshed: false));
        });
        using var services = CreateServices(refresher);
        var worker = CreateWorker(services);

        await worker.RunIterationAsync(CancellationToken.None);

        Assert.Equal([AdditionalFieldTarget.Product, AdditionalFieldTarget.Party], targets);
    }

    [Fact]
    public async Task RunIterationAsync_ContinuesWithPartyWhenProductRefreshFails()
    {
        var refresher = Substitute.For<IAdvancedFieldDefinitionsMaterializationRefresher>();
        refresher.RefreshIfDueAsync(AdditionalFieldTarget.Product, Arg.Any<CancellationToken>()).Returns(Task.FromException<AdvancedFieldDefinitionsMaterializationResult>(new InvalidOperationException("Product definitions failed.")));
        refresher.RefreshIfDueAsync(AdditionalFieldTarget.Party, Arg.Any<CancellationToken>()).Returns(CreateResult(AdditionalFieldTarget.Party, refreshed: true));
        using var services = CreateServices(refresher);
        var worker = CreateWorker(services);

        await worker.RunIterationAsync(CancellationToken.None);

        await refresher.Received(1).RefreshIfDueAsync(AdditionalFieldTarget.Party, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunIterationAsync_DoesNotStartWorkWhenCancellationWasRequested()
    {
        var refresher = Substitute.For<IAdvancedFieldDefinitionsMaterializationRefresher>();
        using var services = CreateServices(refresher);
        var worker = CreateWorker(services);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => worker.RunIterationAsync(cancellation.Token));

        await refresher.DidNotReceiveWithAnyArgs().RefreshIfDueAsync(default, default);
    }

    private static ServiceProvider CreateServices(IAdvancedFieldDefinitionsMaterializationRefresher refresher)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => refresher);
        return services.BuildServiceProvider();
    }

    private static AdvancedFieldDefinitionsMaterializationWorker CreateWorker(ServiceProvider services) => new(services.GetRequiredService<IServiceScopeFactory>(), Substitute.For<ILogger<AdvancedFieldDefinitionsMaterializationWorker>>());

    private static AdvancedFieldDefinitionsMaterializationResult CreateResult(AdditionalFieldTarget target, bool refreshed) => new(new AdvancedFieldDefinitionsDto(target, [], []), refreshed, false);
}
