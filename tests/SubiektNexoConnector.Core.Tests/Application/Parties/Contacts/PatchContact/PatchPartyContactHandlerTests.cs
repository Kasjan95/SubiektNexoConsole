using NSubstitute;
using SubiektNexoConnector.Core.Application.Common;
using SubiektNexoConnector.Core.Application.Parties.Contacts.PatchContact;
using SubiektNexoConnector.Core.Application.Parties.Contacts.Shared;
using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Tests.Application.Parties.Contacts.PatchContact;

public sealed class PatchPartyContactHandlerTests
{
    [Fact]
    public void Handle_NormalizesTextFields_BeforeCallingRepository()
    {
        var repository = Substitute.For<IPartyRepository>();
        var expected = new PartyContactDto(123, false, 1, "Email", "contact@example.com", null);
        repository.PatchPartyContact(Arg.Any<PatchPartyContactCommand>()).Returns(expected);
        var command = new PatchPartyContactCommand(
            "PARTY-001",
            123,
            new Optional<bool>(true),
            new Optional<string?>("  contact@example.com  "),
            new Optional<string?>("   "));

        var result = new PatchPartyContactHandler(repository).Handle(command);

        Assert.Equal(expected, result);
        repository.Received(1).PatchPartyContact(Arg.Is<PatchPartyContactCommand>(patched =>
            patched.IsPrimary.HasValue && patched.IsPrimary.Value &&
            patched.ContactValue.HasValue && patched.ContactValue.Value == "contact@example.com" &&
            patched.ContactDescription.HasValue && patched.ContactDescription.Value == null));
    }

    [Fact]
    public void Handle_ThrowsAndDoesNotCallRepository_WhenNoFieldWasProvided()
    {
        var repository = Substitute.For<IPartyRepository>();
        var command = new PatchPartyContactCommand("PARTY-001", 123, default, default, default);

        var action = () => new PatchPartyContactHandler(repository).Handle(command);

        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Equal("At least one field must be provided.", exception.Message);
        repository.DidNotReceive().PatchPartyContact(Arg.Any<PatchPartyContactCommand>());
    }
}
