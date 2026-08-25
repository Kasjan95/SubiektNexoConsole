using NSubstitute;
using SubiektNexoConnector.Core.Application.Parties.Addresses.DeleteAddress;
using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Tests.Application.Parties.Addresses.DeleteAddress
{
    public sealed class DeletePartyAddressHandlerTests
    {
        [Fact]
        public void Handle_ReturnsDeleted_WhenRepositoryDeletesAddress()
        {
            var repository = Substitute.For<IPartyRepository>();
            var command = new DeletePartyAddressCommand("PARTY-001", 123);
            repository.DeletePartyAddress(command).Returns(DeletePartyResourceResult.Deleted);
            var handler = new DeletePartyAddressHandler(repository);

            var result = handler.Handle(command);

            Assert.Equal(DeletePartyResourceResult.Deleted, result);
            repository.Received(1).DeletePartyAddress(command);
        }

        [Fact]
        public void Handle_ReturnsNotFound_WhenRepositoryDoesNotFindAddress()
        {
            var repository = Substitute.For<IPartyRepository>();
            var command = new DeletePartyAddressCommand("PARTY-001", 123);
            repository.DeletePartyAddress(command).Returns(DeletePartyResourceResult.NotFound);
            var handler = new DeletePartyAddressHandler(repository);

            var result = handler.Handle(command);

            Assert.Equal(DeletePartyResourceResult.NotFound, result);
            repository.Received(1).DeletePartyAddress(command);
        }
    }
}
