using NSubstitute;
using SubiektNexoConnector.Core.Application.Parties;
using SubiektNexoConnector.Core.Application.Parties.Addresses.DeleteAddress;
using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Tests.Application.Parties.Addresses.DeleteAddress
{
    public sealed class DeletePartyAddressHandlerTests
    {
        [Fact]
        public async Task HandleAsync_ReturnsUpdatedPartyAndUpdatesReadModel_WhenRepositoryDeletesAddress()
        {
            var repository = Substitute.For<IPartyRepository>();
            var synchronizer = Substitute.For<IPartyReadModelSynchronizer>();
            var command = new DeletePartyAddressCommand("PARTY-001", 123);
            var party = CreateParty();
            repository.DeletePartyAddress(command).Returns(party);
            var handler = new DeletePartyAddressHandler(repository, synchronizer);

            var result = await handler.HandleAsync(command);

            Assert.Equal(party, result);
            repository.Received(1).DeletePartyAddress(command);
            await synchronizer.Received(1).TryUpsertAsync(party, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task HandleAsync_ReturnsNotFound_WhenRepositoryDoesNotFindAddress()
        {
            var repository = Substitute.For<IPartyRepository>();
            var synchronizer = Substitute.For<IPartyReadModelSynchronizer>();
            var command = new DeletePartyAddressCommand("PARTY-001", 123);
            repository.DeletePartyAddress(command).Returns((PartyDetailsDto?)null);
            var handler = new DeletePartyAddressHandler(repository, synchronizer);

            var result = await handler.HandleAsync(command);

            Assert.Null(result);
            repository.Received(1).DeletePartyAddress(command);
            await synchronizer.DidNotReceive().TryUpsertAsync(Arg.Any<PartyDetailsDto>(), Arg.Any<CancellationToken>());
        }

        private static PartyDetailsDto CreateParty() => new(
            1, "PARTY-001", "Test party", true,
            new PartyClassificationDto(2, "Firma", 7, "Firma", 0, "Standardowy"),
            null, null, "Test party", null, null, null, null, null, [], [], null, null, [], [], [], [],
            new TradeCreditLimitDto(false, null, null, null, null, EmptyLimit(), EmptyLimit(), EmptyLimit()));

        private static DocumentTradeCreditLimitDto EmptyLimit() => new(false, null, null, null);
    }
}
