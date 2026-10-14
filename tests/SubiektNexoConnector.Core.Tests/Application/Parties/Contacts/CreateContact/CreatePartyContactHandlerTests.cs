using NSubstitute;
using SubiektNexoConnector.Core.Application.Parties;
using SubiektNexoConnector.Core.Application.Parties.Contacts.CreateContact;
using SubiektNexoConnector.Core.Application.Parties.CreateParty;
using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Tests.Application.Parties.Contacts.CreateContact;

public sealed class CreatePartyContactHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsUpdatedPartyAndUpdatesReadModel()
    {
        var repository = Substitute.For<IPartyRepository>();
        var synchronizer = Substitute.For<IPartyReadModelSynchronizer>();
        var command = new CreatePartyContactCommand("PARTY-001", new PartyContactInput(3, "contact@example.com", true, "Sales"));
        var party = CreateParty();
        repository.CreatePartyContact(command).Returns(party);
        var handler = new CreatePartyContactHandler(repository, synchronizer);

        var result = await handler.HandleAsync(command);

        Assert.Equal(party, result);
        repository.Received(1).CreatePartyContact(command);
        await synchronizer.Received(1).TryUpsertAsync(party, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_DoesNotUpdateReadModel_WhenPartyDoesNotExist()
    {
        var repository = Substitute.For<IPartyRepository>();
        var synchronizer = Substitute.For<IPartyReadModelSynchronizer>();
        var command = new CreatePartyContactCommand("MISSING", new PartyContactInput(3, "contact@example.com", true, null));
        repository.CreatePartyContact(command).Returns((PartyDetailsDto?)null);
        var handler = new CreatePartyContactHandler(repository, synchronizer);

        var result = await handler.HandleAsync(command);

        Assert.Null(result);
        await synchronizer.DidNotReceive().TryUpsertAsync(Arg.Any<PartyDetailsDto>(), Arg.Any<CancellationToken>());
    }

    private static PartyDetailsDto CreateParty() => new(
        1, "PARTY-001", "Test party", true,
        new PartyClassificationDto(2, "Firma", 7, "Firma", 0, "Standardowy"),
        null, null, "Test party", null, null, null, null, null, [], [], null, null, [], [], [], [],
        new TradeCreditLimitDto(false, null, null, null, null, EmptyLimit(), EmptyLimit(), EmptyLimit()));

    private static DocumentTradeCreditLimitDto EmptyLimit() => new(false, null, null, null);
}
