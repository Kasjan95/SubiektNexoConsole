using NSubstitute;
using SubiektNexoConnector.Core.Application.Common;
using SubiektNexoConnector.Core.Application.Parties;
using SubiektNexoConnector.Core.Application.Parties.Addresses.PatchAddress;
using SubiektNexoConnector.Core.Application.Parties.Addresses.Shared;
using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Tests.Application.Parties.Addresses.PatchAddress;

public sealed class PatchPartyAddressHandlerTests
{
    [Fact]
    public async Task HandleAsync_NormalizesTextFieldsBeforeCallingRepository()
    {
        var repository = Substitute.For<IPartyRepository>();
        var synchronizer = Substitute.For<IPartyReadModelSynchronizer>();
        var party = CreateParty();
        var command = new PatchPartyAddressCommand(
            "PARTY-001", 123,
            new Optional<string?>("  Main street  "),
            default, default,
            new Optional<string?>("   "),
            default, default);
        repository.PatchPartyAddress(Arg.Any<PatchPartyAddressCommand>()).Returns(party);

        await new PatchPartyAddressHandler(repository, synchronizer).HandleAsync(command);

        repository.Received(1).PatchPartyAddress(Arg.Is<PatchPartyAddressCommand>(patched =>
            patched.PartySignature == "PARTY-001" &&
            patched.AddressId == 123 &&
            patched.Street.Value == "Main street" &&
            patched.PostalCode.HasValue &&
            patched.PostalCode.Value == null));
        await synchronizer.Received(1).TryUpsertAsync(party, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenNoFieldWasProvided()
    {
        var repository = Substitute.For<IPartyRepository>();
        var command = new PatchPartyAddressCommand("PARTY-001", 123, default, default, default, default, default, default);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new PatchPartyAddressHandler(repository, Substitute.For<IPartyReadModelSynchronizer>()).HandleAsync(command));

        Assert.Equal("At least one field must be provided.", exception.Message);
        repository.DidNotReceive().PatchPartyAddress(Arg.Any<PatchPartyAddressCommand>());
    }

    private static PartyDetailsDto CreateParty() => new(
        1, "PARTY-001", "Test party", true,
        new PartyClassificationDto(2, "Firma", 7, "Firma", 0, "Standardowy"),
        null, null, "Test party", null, null, null, null, null, [], [], null, null, [], [], [], [],
        new TradeCreditLimitDto(false, null, null, null, null, EmptyLimit(), EmptyLimit(), EmptyLimit()));

    private static DocumentTradeCreditLimitDto EmptyLimit() => new(false, null, null, null);
}
