using NSubstitute;
using SubiektNexoConnector.Core.Application.Parties.Addresses.CreateAddress;
using SubiektNexoConnector.Core.Application.Parties.CreateParty;
using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Tests.Application.Parties.Addresses.CreateAddress;

public sealed class CreatePartyAddressHandlerTests
{
    [Fact]
    public void Handle_ReturnsRepositoryResult()
    {
        var repository = Substitute.For<IPartyRepository>();
        var command = new CreatePartyAddressCommand("PARTY-001", new PartyAddressInput(2, "Street", "1", null, "00-001", "Warsaw", 1));
        var expected = new CreatePartyAddressResult(null!, IsCreated: true);
        repository.CreatePartyAddress(command).Returns(expected);

        var result = new CreatePartyAddressHandler(repository).Handle(command);

        Assert.Same(expected, result);
        repository.Received(1).CreatePartyAddress(command);
    }
}
