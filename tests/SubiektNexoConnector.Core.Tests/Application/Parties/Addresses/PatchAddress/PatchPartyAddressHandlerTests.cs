using NSubstitute;
using SubiektNexoConnector.Core.Application.Common;
using SubiektNexoConnector.Core.Application.Parties.Addresses.PatchAddress;
using SubiektNexoConnector.Core.Application.Parties.Addresses.Shared;
using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Tests.Application.Parties.Addresses.PatchAddress;

public sealed class PatchPartyAddressHandlerTests
{
    [Fact]
    public void Handle_NormalizesTextFieldsBeforeCallingRepository()
    {
        var repository = Substitute.For<IPartyRepository>();
        var command = new PatchPartyAddressCommand(
            "PARTY-001", 123,
            new Optional<string?>("  Main street  "),
            default, default,
            new Optional<string?>("   "),
            default, default);
        repository.PatchPartyAddress(Arg.Any<PatchPartyAddressCommand>()).Returns((PartyAddressDto?)null);

        new PatchPartyAddressHandler(repository).Handle(command);

        repository.Received(1).PatchPartyAddress(Arg.Is<PatchPartyAddressCommand>(patched =>
            patched.PartySignature == "PARTY-001" &&
            patched.AddressId == 123 &&
            patched.Street.Value == "Main street" &&
            patched.PostalCode.HasValue &&
            patched.PostalCode.Value == null));
    }

    [Fact]
    public void Handle_Throws_WhenNoFieldWasProvided()
    {
        var repository = Substitute.For<IPartyRepository>();
        var command = new PatchPartyAddressCommand("PARTY-001", 123, default, default, default, default, default, default);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new PatchPartyAddressHandler(repository).Handle(command));

        Assert.Equal("At least one field must be provided.", exception.Message);
        repository.DidNotReceive().PatchPartyAddress(Arg.Any<PatchPartyAddressCommand>());
    }
}
