using NSubstitute;
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.GetAdvancedFieldDefinitions;
using SubiektNexoConnector.Core.Application.AdditionalFields.Materialization;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Core.Tests.Application.AdvancedFieldDefinitions;

public sealed class AdvancedFieldDefinitionsMaterializationRefresherTests
{
    private readonly IAdditionalFieldDefinitionRepository _source = Substitute.For<IAdditionalFieldDefinitionRepository>();
    private readonly IAdvancedFieldDefinitionsReadModelReader _reader = Substitute.For<IAdvancedFieldDefinitionsReadModelReader>();
    private readonly IAdvancedFieldDefinitionsReadModelWriter _writer = Substitute.For<IAdvancedFieldDefinitionsReadModelWriter>();
    private readonly IMaterializationCheckpointStore _checkpointStore = Substitute.For<IMaterializationCheckpointStore>();
    private readonly IMaterializationPolicyStore _policyStore = Substitute.For<IMaterializationPolicyStore>();

    [Fact]
    public async Task GetCurrentAsync_ReturnsFreshSnapshotWithoutOpeningSource()
    {
        var snapshot = CreateSnapshot("cached");
        ConfigureState(DateTime.UtcNow, snapshot);
        var refresher = CreateRefresher();

        var result = await refresher.GetCurrentAsync(AdditionalFieldTarget.Product);

        Assert.Same(snapshot, result);
        _source.DidNotReceiveWithAnyArgs().GetAdvancedFieldDefinitions(default!, default);
        await _writer.DidNotReceiveWithAnyArgs().ReplaceAsync(default!, default);
    }

    [Fact]
    public async Task GetCurrentAsync_RefreshesMissingSnapshotAndCompletesCheckpoint()
    {
        var source = CreateSnapshot("source");
        var savedCheckpoints = new List<MaterializationCheckpoint>();
        ConfigureState(null, new AdvancedFieldDefinitionsDto(AdditionalFieldTarget.Product, [], []));
        _source.GetAdvancedFieldDefinitions(Arg.Any<GetAdvancedFieldDefinitionsQuery>(), Arg.Any<CancellationToken>()).Returns(source);
        _checkpointStore.SaveAsync(Arg.Do<MaterializationCheckpoint>(savedCheckpoints.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var refresher = CreateRefresher();

        var result = await refresher.GetCurrentAsync(AdditionalFieldTarget.Product);

        Assert.Same(source, result);
        await _writer.Received(1).ReplaceAsync(source, Arg.Any<CancellationToken>());
        Assert.Contains(savedCheckpoints, checkpoint => checkpoint.LastCycleCompletedAtUtc is not null && checkpoint.CycleStartedAtUtc is null && checkpoint.LastError is null);
    }

    [Fact]
    public async Task RefreshAsync_DoesNotRewriteEquivalentSnapshot()
    {
        var snapshot = CreateSnapshot("same");
        ConfigureState(DateTime.UtcNow.AddHours(-1), snapshot);
        _source.GetAdvancedFieldDefinitions(Arg.Any<GetAdvancedFieldDefinitionsQuery>(), Arg.Any<CancellationToken>()).Returns(CreateSnapshot("same"));
        var refresher = CreateRefresher();

        var result = await refresher.RefreshAsync(AdditionalFieldTarget.Product);

        Assert.True(result.Refreshed);
        Assert.False(result.Changed);
        await _writer.DidNotReceiveWithAnyArgs().ReplaceAsync(default!, default);
    }

    [Fact]
    public async Task RefreshIfDueAsync_UsesRefreshAheadWindow()
    {
        var snapshot = CreateSnapshot("same");
        ConfigureState(DateTime.UtcNow.AddSeconds(-2581), snapshot);
        _source.GetAdvancedFieldDefinitions(Arg.Any<GetAdvancedFieldDefinitionsQuery>(), Arg.Any<CancellationToken>()).Returns(snapshot);
        var refresher = CreateRefresher();

        var result = await refresher.RefreshIfDueAsync(AdditionalFieldTarget.Product);

        Assert.True(result.Refreshed);
        _source.Received(1).GetAdvancedFieldDefinitions(Arg.Any<GetAdvancedFieldDefinitionsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetCurrentAsync_ReturnsStaleSnapshotWhenSourceRefreshFails()
    {
        var stale = CreateSnapshot("stale");
        var savedCheckpoints = new List<MaterializationCheckpoint>();
        ConfigureState(DateTime.UtcNow.AddHours(-1), stale);
        _source.GetAdvancedFieldDefinitions(Arg.Any<GetAdvancedFieldDefinitionsQuery>(), Arg.Any<CancellationToken>()).Returns(_ => throw new InvalidOperationException("Sfera unavailable."));
        _checkpointStore.SaveAsync(Arg.Do<MaterializationCheckpoint>(savedCheckpoints.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var refresher = CreateRefresher();

        var result = await refresher.GetCurrentAsync(AdditionalFieldTarget.Product);

        Assert.Same(stale, result);
        Assert.Contains(savedCheckpoints, checkpoint => checkpoint.LastErrorAtUtc is not null && checkpoint.LastError?.Contains("Sfera unavailable.", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task GetCurrentAsync_PropagatesSourceFailureWhenSnapshotWasNeverLoaded()
    {
        ConfigureState(null, new AdvancedFieldDefinitionsDto(AdditionalFieldTarget.Product, [], []));
        _source.GetAdvancedFieldDefinitions(Arg.Any<GetAdvancedFieldDefinitionsQuery>(), Arg.Any<CancellationToken>()).Returns(_ => throw new InvalidOperationException("Sfera unavailable."));
        var refresher = CreateRefresher();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => refresher.GetCurrentAsync(AdditionalFieldTarget.Product));

        Assert.Equal("Sfera unavailable.", exception.Message);
    }

    private AdvancedFieldDefinitionsMaterializationRefresher CreateRefresher() => new(_source, _reader, _writer, _checkpointStore, _policyStore);

    private void ConfigureState(DateTime? lastCompletedAtUtc, AdvancedFieldDefinitionsDto snapshot)
    {
        var definition = MaterializationPolicyDefinitions.AdvancedFieldDefinitionsProduct;
        var checkpoint = new MaterializationCheckpoint(definition.Id, null, null, null, lastCompletedAtUtc, null, null);
        _policyStore.GetOrCreateAsync(definition, Arg.Any<CancellationToken>()).Returns(CreatePolicy(definition));
        _checkpointStore.GetOrCreateAsync(definition.Id, Arg.Any<CancellationToken>()).Returns(checkpoint);
        _reader.GetAsync(AdditionalFieldTarget.Product, Arg.Any<CancellationToken>()).Returns(snapshot);
    }

    private static MaterializationPolicy CreatePolicy(MaterializationPolicyDefinition definition) => new(definition.Id, definition.Name, definition.Lifetime, definition.RefreshAhead, definition.BatchSize, definition.CandidateSelection, definition.Requirements, DateTime.UtcNow);

    private static AdvancedFieldDefinitionsDto CreateSnapshot(string fieldId) => new(AdditionalFieldTarget.Product, [], [new AdvancedFieldDefinitionDto(fieldId, fieldId, null, AdvancedFieldDataType.Text, false, true, true, true, null, null, null, null, null)]);
}
