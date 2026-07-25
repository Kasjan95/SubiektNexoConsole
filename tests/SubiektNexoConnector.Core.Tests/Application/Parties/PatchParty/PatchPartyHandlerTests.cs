using NSubstitute;
using SubiektNexoConnector.Core.Application.Common;
using SubiektNexoConnector.Core.Application.Parties.PatchParty;
using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Tests.Application.Parties.PatchParty;

public class PatchPartyHandlerTests
{
    [Fact]
    public void Handle_NormalizesFieldsAndReturnsUpdatedSignature()
    {
        var repository = Substitute.For<IPartyRepository>();
        var command = new PatchPartyCommand(
            PartySignature: "PARTY-001",
            Signature: new Optional<string>("  PARTY-002  "),
            DisplayName: default,
            IsActive: new Optional<bool>(false),
            FirstName: default,
            LastName: default,
            CompanyName: default,
            TaxId: default,
            EuTaxId: default,
            BusinessRegistryNumber: default,
            NationalCourtRegisterNumber: default,
            PartyGroup: default,
            Industries: default,
            Features: default,
            Notes: new Optional<string?>("  Important customer  "));
        repository.Patch(Arg.Any<PatchPartyCommand>()).Returns("PARTY-002");

        var result = new PatchPartyHandler(repository).Handle(command);

        Assert.Equal("PARTY-002", result);
        repository.Received(1).Patch(Arg.Is<PatchPartyCommand>(patchedCommand =>
            patchedCommand.PartySignature == "PARTY-001" &&
            patchedCommand.Signature.Value == "PARTY-002" &&
            patchedCommand.IsActive.HasValue &&
            !patchedCommand.IsActive.Value &&
            patchedCommand.Notes.Value == "Important customer"));
    }

    [Fact]
    public void Handle_NormalizesWhitespaceOptionalValueToNull()
    {
        var repository = Substitute.For<IPartyRepository>();
        var command = new PatchPartyCommand(
            "PARTY-001", default, default, default, default, default, default, default, default,
            default, default, default, default, default, new Optional<string?>("   "));
        repository.Patch(Arg.Any<PatchPartyCommand>()).Returns("PARTY-001");

        new PatchPartyHandler(repository).Handle(command);

        repository.Received(1).Patch(Arg.Is<PatchPartyCommand>(patchedCommand =>
            patchedCommand.Notes.HasValue && patchedCommand.Notes.Value == null));
    }

    [Fact]
    public void Handle_Throws_WhenNoFieldWasProvided()
    {
        var repository = Substitute.For<IPartyRepository>();
        var command = new PatchPartyCommand(
            "PARTY-001", default, default, default, default, default, default, default, default,
            default, default, default, default, default, default);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new PatchPartyHandler(repository).Handle(command));

        Assert.Equal("At least one field must be provided.", exception.Message);
        repository.DidNotReceive().Patch(Arg.Any<PatchPartyCommand>());
    }

    [Fact]
    public void Handle_NormalizesAndDeduplicatesTextLists()
    {
        var repository = Substitute.For<IPartyRepository>();
        var command = new PatchPartyCommand(
            "PARTY-001", default, default, default, default, default, default, default, default,
            default, default, default,
            new Optional<IReadOnlyCollection<string>>(new[] { "  Retail  ", "retail", "Wholesale" }),
            new Optional<IReadOnlyCollection<string>>(new[] { " VIP ", "vip" }),
            default);
        repository.Patch(Arg.Any<PatchPartyCommand>()).Returns("PARTY-001");

        new PatchPartyHandler(repository).Handle(command);

        repository.Received(1).Patch(Arg.Is<PatchPartyCommand>(patchedCommand =>
            patchedCommand.Industries.Value!.SequenceEqual(new[] { "Retail", "Wholesale" }) &&
            patchedCommand.Features.Value!.SequenceEqual(new[] { "VIP" })));
    }
}
