using NSubstitute;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Parties;
using SubiektNexoConnector.Core.Application.Parties.Addresses.Shared;
using SubiektNexoConnector.Core.Application.Parties.Contacts.Shared;
using SubiektNexoConnector.Core.Application.Parties.CreateParty;
using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Tests.Application.Parties.CreateParty;

public class CreatePartyHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsCreatedPartyAndUpdatesReadModel()
    {
        var repository = Substitute.For<IPartyRepository>();
        var synchronizer = Substitute.For<IPartyReadModelSynchronizer>();
        var command = CreateCommand();
        var party = CreateParty();
        repository.CreateParty(command).Returns(party);
        synchronizer.TryUpsertAsync(party, Arg.Any<CancellationToken>()).Returns(true);
        var handler = new CreatePartyHandler(repository, synchronizer);

        var result = await handler.HandleAsync(command);

        Assert.Equal(party, result);
        repository.Received(1).CreateParty(command);
        await synchronizer.Received(1).TryUpsertAsync(party, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_DoesNotUpdateReadModel_WhenRepositoryThrows()
    {
        var repository = Substitute.For<IPartyRepository>();
        var synchronizer = Substitute.For<IPartyReadModelSynchronizer>();
        var command = CreateCommand();
        repository.CreateParty(command).Returns(_ => throw new InvalidOperationException("Party already exists."));
        var handler = new CreatePartyHandler(repository, synchronizer);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(command));

        Assert.Equal("Party already exists.", exception.Message);
        await synchronizer.DidNotReceive().TryUpsertAsync(Arg.Any<PartyDetailsDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ReturnsCreatedParty_WhenReadModelUpdateFails()
    {
        var repository = Substitute.For<IPartyRepository>();
        var synchronizer = Substitute.For<IPartyReadModelSynchronizer>();
        var command = CreateCommand();
        var party = CreateParty();
        repository.CreateParty(command).Returns(party);
        synchronizer.TryUpsertAsync(party, Arg.Any<CancellationToken>()).Returns(false);
        var handler = new CreatePartyHandler(repository, synchronizer);

        var result = await handler.HandleAsync(command);

        Assert.Equal(party, result);
    }

    private static CreatePartyCommand CreateCommand() => new("Test party", 2, 7, "PARTY-001", null, null, "Test party", null, null, null, null, null, [], [], null, [], [], []);

    private static PartyDetailsDto CreateParty() => new(
        1,
        "PARTY-001",
        "Test party",
        true,
        new PartyClassificationDto(2, "Firma", 7, "Firma", 0, "Standardowy"),
        null,
        null,
        "Test party",
        null,
        null,
        null,
        null,
        null,
        [],
        [],
        null,
        null,
        Array.Empty<AdditionalFieldValueDto>(),
        Array.Empty<AdditionalFieldValueDto>(),
        Array.Empty<PartyAddressDto>(),
        Array.Empty<PartyContactDto>(),
        new TradeCreditLimitDto(false, null, null, null, null, EmptyDocumentLimit(), EmptyDocumentLimit(), EmptyDocumentLimit()));

    private static DocumentTradeCreditLimitDto EmptyDocumentLimit() => new(false, null, null, null);
}
