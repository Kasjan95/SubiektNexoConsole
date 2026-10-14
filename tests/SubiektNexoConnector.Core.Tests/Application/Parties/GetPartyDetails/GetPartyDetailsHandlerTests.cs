using NSubstitute;
using SubiektNexoConnector.Core.Application.Parties;
using SubiektNexoConnector.Core.Application.Parties.GetPartyDetails;
using SubiektNexoConnector.Core.Application.Parties.Shared;
using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Core.Tests.Application.Parties.GetPartyDetails;

public sealed class GetPartyDetailsHandlerTests
{
    private readonly IPartyReadModelReader _reader = Substitute.For<IPartyReadModelReader>();
    private readonly IPartyMaterializationRefresher _refresher = Substitute.For<IPartyMaterializationRefresher>();
    private readonly IMaterializationFreshnessEvaluator _freshnessEvaluator = Substitute.For<IMaterializationFreshnessEvaluator>();

    [Fact]
    public async Task HandleAsync_ReturnsFreshMaterializationWithoutOpeningNexo()
    {
        var party = CreateParty();
        var refreshedAtUtc = DateTime.UtcNow.AddMinutes(-1);
        _reader.GetDetailsAsync(party.Signature, Arg.Any<CancellationToken>()).Returns(new MaterializedPartyDetails(party, refreshedAtUtc));
        _freshnessEvaluator.EvaluateAsync(MaterializationPolicyDefinitions.Parties, refreshedAtUtc, Arg.Any<CancellationToken>()).Returns(new MaterializationMetadata(MaterializationStatus.Fresh, refreshedAtUtc));
        var handler = CreateHandler();

        var result = await handler.HandleAsync(new GetPartyDetailsQuery(party.Signature));

        Assert.Equal(party, result.Value);
        Assert.Equal(MaterializationStatus.Fresh, result.Metadata.Status);
        await _refresher.DidNotReceiveWithAnyArgs().RefreshOneAsync(default!, default);
    }

    [Fact]
    public async Task HandleAsync_RefreshesMissingParty_AndReadsItFromSqlite()
    {
        var party = CreateParty();
        var refreshedAtUtc = DateTime.UtcNow;
        _reader.GetDetailsAsync(party.Signature, Arg.Any<CancellationToken>()).Returns((MaterializedPartyDetails?)null, new MaterializedPartyDetails(party, refreshedAtUtc));
        var handler = CreateHandler();

        var result = await handler.HandleAsync(new GetPartyDetailsQuery(party.Signature));

        Assert.Equal(party, result.Value);
        Assert.Equal(MaterializationStatus.Fresh, result.Metadata.Status);
        await _refresher.Received(1).RefreshOneAsync(party.Signature, Arg.Any<CancellationToken>());
        await _reader.Received(2).GetDetailsAsync(party.Signature, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ReturnsStaleParty_WhenRefreshFails()
    {
        var party = CreateParty();
        var refreshedAtUtc = DateTime.UtcNow.AddHours(-1);
        _reader.GetDetailsAsync(party.Signature, Arg.Any<CancellationToken>()).Returns(new MaterializedPartyDetails(party, refreshedAtUtc));
        _freshnessEvaluator.EvaluateAsync(MaterializationPolicyDefinitions.Parties, refreshedAtUtc, Arg.Any<CancellationToken>()).Returns(new MaterializationMetadata(MaterializationStatus.Stale, refreshedAtUtc));
        _refresher.RefreshOneAsync(party.Signature, Arg.Any<CancellationToken>()).Returns(_ => throw new InvalidOperationException("Nexo unavailable"));
        var handler = CreateHandler();

        var result = await handler.HandleAsync(new GetPartyDetailsQuery(party.Signature));

        Assert.Equal(party, result.Value);
        Assert.Equal(MaterializationStatus.Stale, result.Metadata.Status);
        Assert.Equal(refreshedAtUtc, result.Metadata.LastSuccessAtUtc);
    }

    private GetPartyDetailsHandler CreateHandler() => new(_reader, _refresher, _freshnessEvaluator);

    private static PartyDetailsDto CreateParty() => new(
        1,
        "PARTY-1",
        "Party",
        true,
        new PartyClassificationDto(2, "Firma", 7, "Firma", 0, "Standardowy"),
        null,
        null,
        "Party",
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
        new TradeCreditLimitDto(false, null, null, null, null, EmptyLimit(), EmptyLimit(), EmptyLimit()));

    private static DocumentTradeCreditLimitDto EmptyLimit() => new(false, null, null, null);
}
