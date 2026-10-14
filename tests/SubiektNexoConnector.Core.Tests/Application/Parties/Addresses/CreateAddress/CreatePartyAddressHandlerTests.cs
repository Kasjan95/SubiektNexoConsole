using NSubstitute;
using SubiektNexoConnector.Core.Application.Parties;
using SubiektNexoConnector.Core.Application.Parties.Addresses.CreateAddress;
using SubiektNexoConnector.Core.Application.Parties.CreateParty;
using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Tests.Application.Parties.Addresses.CreateAddress;

public sealed class CreatePartyAddressHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsRepositoryResultAndUpdatesReadModel()
    {
        var repository = Substitute.For<IPartyRepository>();
        var synchronizer = Substitute.For<IPartyReadModelSynchronizer>();
        var command = new CreatePartyAddressCommand("PARTY-001", new PartyAddressInput(2, "Street", "1", null, "00-001", "Warsaw", 1));
        var expected = new CreatePartyAddressResult(CreateParty(), IsCreated: true);
        repository.CreatePartyAddress(command).Returns(expected);

        var result = await new CreatePartyAddressHandler(repository, synchronizer).HandleAsync(command);

        Assert.Same(expected, result);
        repository.Received(1).CreatePartyAddress(command);
        await synchronizer.Received(1).TryUpsertAsync(expected.Party, Arg.Any<CancellationToken>());
    }

    private static PartyDetailsDto CreateParty() => new(
        1, "PARTY-001", "Test party", true,
        new PartyClassificationDto(2, "Firma", 7, "Firma", 0, "Standardowy"),
        null, null, "Test party", null, null, null, null, null, [], [], null, null, [], [], [], [],
        new TradeCreditLimitDto(false, null, null, null, null, EmptyLimit(), EmptyLimit(), EmptyLimit()));

    private static DocumentTradeCreditLimitDto EmptyLimit() => new(false, null, null, null);
}
