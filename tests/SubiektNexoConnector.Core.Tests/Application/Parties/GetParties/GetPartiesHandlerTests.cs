using NSubstitute;
using SubiektNexoConnector.Core.Application.Parties;
using SubiektNexoConnector.Core.Application.Parties.GetParties;
using SubiektNexoConnector.Core.Application.Parties.Shared;
using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Core.Tests.Application.Parties.GetParties;

public sealed class GetPartiesHandlerTests
{
    private readonly IPartyReadModelReader _reader = Substitute.For<IPartyReadModelReader>();
    private readonly IMaterializationCheckpointStore _checkpointStore = Substitute.For<IMaterializationCheckpointStore>();
    private readonly IMaterializationFreshnessEvaluator _freshnessEvaluator = Substitute.For<IMaterializationFreshnessEvaluator>();

    [Fact]
    public async Task HandleAsync_ReturnsPartiesFromCompleteMaterialization()
    {
        var expectedParties = new List<PartyBasicDto>
        {
            new(1, "SZEF", "Kamil Kasjaniuk", new PartyClassificationDto(1, "Osoba", 1, "Pracownik", 0, "Standardowy"), null, true, "Kamil", "Kasjaniuk", null),
            new(2, "FIRMA", "MULTIPROJEKT Kamil Kasjaniuk", new PartyClassificationDto(2, "Firma", 11, "Firma", 0, "Standardowy"), "6372205552", true, null, null, "MULTIPROJEKT Kamil Kasjaniuk")
        };
        var query = new GetPartiesQuery { CustomerStatus = PartyCustomerStatusFilter.Standard, Type = 2, Search = "multi", Page = 2, PageSize = 50 };
        var completedAtUtc = DateTime.UtcNow.AddMinutes(-5);
        ConfigureCompleteMaterialization(completedAtUtc);
        _reader.GetAllAsync(query, Arg.Any<CancellationToken>()).Returns(expectedParties);
        var handler = CreateHandler();

        var result = await handler.HandleAsync(query);

        Assert.Equal(expectedParties, result.Value);
        Assert.Equal(MaterializationStatus.Fresh, result.Metadata.Status);
        await _reader.Received(1).GetAllAsync(query, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ReturnsUnavailableWithoutReadingPartialList_WhenFirstCycleHasNotCompleted()
    {
        _checkpointStore.GetOrCreateAsync(MaterializationPolicyDefinitions.Parties.Id, Arg.Any<CancellationToken>()).Returns(CreateCheckpoint(null));
        var handler = CreateHandler();

        var result = await handler.HandleAsync(new GetPartiesQuery());

        Assert.Null(result.Value);
        Assert.Equal(MaterializationStatus.Unavailable, result.Metadata.Status);
        await _reader.DidNotReceiveWithAnyArgs().GetAllAsync(default!, default);
    }

    [Fact]
    public async Task HandleAsync_ThrowsArgumentNullException_WhenQueryIsNull()
    {
        var handler = CreateHandler();

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.HandleAsync(null!));
    }

    private GetPartiesHandler CreateHandler() => new(_reader, _checkpointStore, _freshnessEvaluator);

    private void ConfigureCompleteMaterialization(DateTime completedAtUtc)
    {
        var definition = MaterializationPolicyDefinitions.Parties;
        _checkpointStore.GetOrCreateAsync(definition.Id, Arg.Any<CancellationToken>()).Returns(CreateCheckpoint(completedAtUtc));
        _freshnessEvaluator.EvaluateAsync(definition, completedAtUtc, Arg.Any<CancellationToken>()).Returns(new MaterializationMetadata(MaterializationStatus.Fresh, completedAtUtc));
    }

    private static MaterializationCheckpoint CreateCheckpoint(DateTime? completedAtUtc) => new(MaterializationPolicyDefinitions.Parties.Id, null, null, null, completedAtUtc, null, null);
}
