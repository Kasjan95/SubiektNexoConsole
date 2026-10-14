using NSubstitute;
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.Materialization;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Parties;
using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Tests.Application.Parties.Materialization;

public sealed class PartyMaterializationRefresherTests
{
    private readonly IPartyRepository _parties = Substitute.For<IPartyRepository>();
    private readonly IAdvancedFieldDefinitionsReadModelReader _fieldDefinitions = Substitute.For<IAdvancedFieldDefinitionsReadModelReader>();
    private readonly IPartyReadModelWriter _writer = Substitute.For<IPartyReadModelWriter>();

    [Fact]
    public async Task RefreshOneAsync_UsesMaterializedDefinitions_AndUpsertsParty()
    {
        var definition = CreateDefinition();
        var party = CreateParty(11);
        _fieldDefinitions.GetAsync(AdditionalFieldTarget.Party, Arg.Any<CancellationToken>()).Returns(new AdvancedFieldDefinitionsDto(AdditionalFieldTarget.Party, [], [definition]));
        _parties.GetDetailsForMaterialization(party.Signature, Arg.Any<IReadOnlyCollection<AdvancedFieldDefinitionDto>>(), Arg.Any<CancellationToken>()).Returns(party);
        var refresher = CreateRefresher();

        await refresher.RefreshOneAsync(party.Signature);

        _parties.Received(1).GetDetailsForMaterialization(
            party.Signature,
            Arg.Is<IReadOnlyCollection<AdvancedFieldDefinitionDto>>(definitions => definitions.Count == 1 && definitions.Single() == definition),
            Arg.Any<CancellationToken>());
        await _writer.Received(1).UpsertAsync(
            party,
            Arg.Is<IReadOnlyDictionary<string, AdvancedFieldDefinitionDto>>(definitions => definitions[definition.Id] == definition),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshOneAsync_DeletesStaleParty_WhenItNoLongerExistsInNexo()
    {
        const string signature = "MISSING";
        _fieldDefinitions.GetAsync(AdditionalFieldTarget.Party, Arg.Any<CancellationToken>()).Returns(new AdvancedFieldDefinitionsDto(AdditionalFieldTarget.Party, [], []));
        _parties.GetDetailsForMaterialization(signature, Arg.Any<IReadOnlyCollection<AdvancedFieldDefinitionDto>>(), Arg.Any<CancellationToken>()).Returns((PartyDetailsDto?)null);
        var refresher = CreateRefresher();

        await refresher.RefreshOneAsync(signature);

        await _writer.Received(1).DeleteAsync(signature, Arg.Any<CancellationToken>());
        await _writer.DidNotReceiveWithAnyArgs().UpsertAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task RefreshNextBatchAsync_LoadsDefinitionsFromReadModel_AndPersistsOneBatch()
    {
        var definition = CreateDefinition();
        var definitions = new AdvancedFieldDefinitionsDto(AdditionalFieldTarget.Party, [], [definition]);
        var parties = new[] { CreateParty(11), CreateParty(12) };
        _fieldDefinitions.GetAsync(AdditionalFieldTarget.Party, Arg.Any<CancellationToken>()).Returns(definitions);
        _parties.GetDetailsBatchAfterId(10, 2, Arg.Any<IReadOnlyCollection<AdvancedFieldDefinitionDto>>(), Arg.Any<CancellationToken>()).Returns(new PartyMaterializationSourceBatch(parties, 12, true));
        var refresher = CreateRefresher();
        var startedAtUtc = DateTime.UtcNow;

        var result = await refresher.RefreshNextBatchAsync(10, 2);

        var finishedAtUtc = DateTime.UtcNow;
        Assert.Equal(new PartyMaterializationBatchResult(2, 12, false), result);
        await _fieldDefinitions.Received(1).GetAsync(AdditionalFieldTarget.Party, Arg.Any<CancellationToken>());
        _parties.Received(1).GetDetailsBatchAfterId(
            10,
            2,
            Arg.Is<IReadOnlyCollection<AdvancedFieldDefinitionDto>>(items => items.Count == 1 && items.Single() == definition),
            Arg.Any<CancellationToken>());
        await _writer.Received(1).UpsertBatchAsync(
            parties,
            Arg.Is<IReadOnlyDictionary<string, AdvancedFieldDefinitionDto>>(items => items.Count == 1 && items[definition.Id] == definition),
            Arg.Is<DateTime>(value => value >= startedAtUtc && value <= finishedAtUtc),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshNextBatchAsync_ReportsCompletedCycle_AndDoesNotWriteEmptyBatch()
    {
        _fieldDefinitions.GetAsync(AdditionalFieldTarget.Party, Arg.Any<CancellationToken>()).Returns(new AdvancedFieldDefinitionsDto(AdditionalFieldTarget.Party, [], []));
        _parties.GetDetailsBatchAfterId(42, 50, Arg.Any<IReadOnlyCollection<AdvancedFieldDefinitionDto>>(), Arg.Any<CancellationToken>()).Returns(new PartyMaterializationSourceBatch([], 42, false));
        var refresher = CreateRefresher();

        var result = await refresher.RefreshNextBatchAsync(42, 50);

        Assert.Equal(new PartyMaterializationBatchResult(0, 42, true), result);
        await _writer.DidNotReceiveWithAnyArgs().UpsertBatchAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task CompleteCycleAsync_RemovesPartiesNotSeenDuringCycle()
    {
        var cycleStartedAtUtc = new DateTime(2026, 10, 13, 9, 0, 0, DateTimeKind.Utc);
        _writer.DeleteNotRefreshedSinceAsync(cycleStartedAtUtc, Arg.Any<CancellationToken>()).Returns(2);
        var refresher = CreateRefresher();

        var removedCount = await refresher.CompleteCycleAsync(cycleStartedAtUtc);

        Assert.Equal(2, removedCount);
        await _writer.Received(1).DeleteNotRefreshedSinceAsync(cycleStartedAtUtc, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshNextBatchAsync_PropagatesCancellationToken()
    {
        using var cancellation = new CancellationTokenSource();
        var cancellationToken = cancellation.Token;
        _fieldDefinitions.GetAsync(AdditionalFieldTarget.Party, cancellationToken).Returns(new AdvancedFieldDefinitionsDto(AdditionalFieldTarget.Party, [], []));
        _parties.GetDetailsBatchAfterId(null, 10, Arg.Any<IReadOnlyCollection<AdvancedFieldDefinitionDto>>(), cancellationToken).Returns(new PartyMaterializationSourceBatch([], null, false));
        var refresher = CreateRefresher();

        await refresher.RefreshNextBatchAsync(null, 10, cancellationToken);

        await _fieldDefinitions.Received(1).GetAsync(AdditionalFieldTarget.Party, cancellationToken);
        _parties.Received(1).GetDetailsBatchAfterId(null, 10, Arg.Any<IReadOnlyCollection<AdvancedFieldDefinitionDto>>(), cancellationToken);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RefreshNextBatchAsync_RejectsNonPositiveBatchSize(int batchSize)
    {
        var refresher = CreateRefresher();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => refresher.RefreshNextBatchAsync(null, batchSize));

        await _fieldDefinitions.DidNotReceiveWithAnyArgs().GetAsync(default, default);
        _parties.DidNotReceiveWithAnyArgs().GetDetailsBatchAfterId(default, default, default!, default);
    }

    private PartyMaterializationRefresher CreateRefresher() => new(_parties, _fieldDefinitions, _writer);

    private static PartyDetailsDto CreateParty(int id) => new(
        id,
        $"PARTY-{id}",
        $"Party {id}",
        true,
        new PartyClassificationDto(2, "Firma", 7, "Firma", 0, "Standardowy"),
        null,
        null,
        $"Party {id}",
        null,
        null,
        null,
        null,
        null,
        [],
        [],
        null,
        null,
        [],
        [],
        [],
        [],
        new TradeCreditLimitDto(false, null, null, null, null, EmptyDocumentLimit(), EmptyDocumentLimit(), EmptyDocumentLimit()));

    private static DocumentTradeCreditLimitDto EmptyDocumentLimit() => new(false, null, null, null);

    private static AdvancedFieldDefinitionDto CreateDefinition() => new("field-id", "Field", null, AdvancedFieldDataType.Text, false, true, true, false, null, null, null, null, null);
}
