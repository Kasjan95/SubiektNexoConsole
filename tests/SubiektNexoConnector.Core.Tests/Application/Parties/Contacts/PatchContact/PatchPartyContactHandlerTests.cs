using NSubstitute;
using SubiektNexoConnector.Core.Application.Common;
using SubiektNexoConnector.Core.Application.Parties;
using SubiektNexoConnector.Core.Application.Parties.Contacts.PatchContact;
using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Tests.Application.Parties.Contacts.PatchContact;

public sealed class PatchPartyContactHandlerTests
{
    [Fact]
    public async Task HandleAsync_NormalizesTextFieldsAndUpdatesReadModel()
    {
        var repository = Substitute.For<IPartyRepository>();
        var synchronizer = Substitute.For<IPartyReadModelSynchronizer>();
        var expected = CreateParty();
        repository.PatchPartyContact(Arg.Any<PatchPartyContactCommand>()).Returns(expected);
        var command = new PatchPartyContactCommand(
            "PARTY-001",
            123,
            new Optional<bool>(true),
            new Optional<string?>("  contact@example.com  "),
            new Optional<string?>("   "));

        var result = await new PatchPartyContactHandler(repository, synchronizer).HandleAsync(command);

        Assert.Equal(expected, result);
        repository.Received(1).PatchPartyContact(Arg.Is<PatchPartyContactCommand>(patched =>
            patched.IsPrimary.HasValue && patched.IsPrimary.Value &&
            patched.ContactValue.HasValue && patched.ContactValue.Value == "contact@example.com" &&
            patched.ContactDescription.HasValue && patched.ContactDescription.Value == null));
        await synchronizer.Received(1).TryUpsertAsync(expected, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ThrowsAndDoesNotCallRepository_WhenNoFieldWasProvided()
    {
        var repository = Substitute.For<IPartyRepository>();
        var command = new PatchPartyContactCommand("PARTY-001", 123, default, default, default);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new PatchPartyContactHandler(repository, Substitute.For<IPartyReadModelSynchronizer>()).HandleAsync(command));
        Assert.Equal("At least one field must be provided.", exception.Message);
        repository.DidNotReceive().PatchPartyContact(Arg.Any<PatchPartyContactCommand>());
    }

    private static PartyDetailsDto CreateParty() => new(
        1, "PARTY-001", "Test party", true,
        new PartyClassificationDto(2, "Firma", 7, "Firma", 0, "Standardowy"),
        null, null, "Test party", null, null, null, null, null, [], [], null, null, [], [], [], [],
        new TradeCreditLimitDto(false, null, null, null, null, EmptyLimit(), EmptyLimit(), EmptyLimit()));

    private static DocumentTradeCreditLimitDto EmptyLimit() => new(false, null, null, null);
}
