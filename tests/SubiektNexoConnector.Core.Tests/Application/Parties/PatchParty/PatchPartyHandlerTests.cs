using NSubstitute;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Common;
using SubiektNexoConnector.Core.Application.Parties;
using SubiektNexoConnector.Core.Application.Parties.PatchParty;
using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Tests.Application.Parties.PatchParty;

public class PatchPartyHandlerTests
{
    [Fact]
    public async Task HandleAsync_NormalizesFieldsReturnsUpdatedPartyAndUpdatesReadModel()
    {
        var repository = Substitute.For<IPartyRepository>();
        var synchronizer = Substitute.For<IPartyReadModelSynchronizer>();
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
            PartyGroupId: default,
            IndustryIds: default,
            FeatureIds: default,
            Notes: new Optional<string?>("  Important customer  "));
        var party = CreateParty("PARTY-002");
        repository.PatchParty(Arg.Any<PatchPartyCommand>()).Returns(party);

        var result = await new PatchPartyHandler(repository, synchronizer).HandleAsync(command);

        Assert.Equal(party, result);
        repository.Received(1).PatchParty(Arg.Is<PatchPartyCommand>(patchedCommand =>
            patchedCommand.PartySignature == "PARTY-001" &&
            patchedCommand.Signature.Value == "PARTY-002" &&
            patchedCommand.IsActive.HasValue &&
            !patchedCommand.IsActive.Value &&
            patchedCommand.Notes.Value == "Important customer"));
        await synchronizer.Received(1).TryUpsertAsync(party, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NormalizesWhitespaceOptionalValueToNull()
    {
        var repository = Substitute.For<IPartyRepository>();
        var command = new PatchPartyCommand(
            "PARTY-001", default, default, default, default, default, default, default, default,
            default, default, default, default, default, new Optional<string?>("   "));
        repository.PatchParty(Arg.Any<PatchPartyCommand>()).Returns(CreateParty("PARTY-001"));

        await CreateHandler(repository).HandleAsync(command);

        repository.Received(1).PatchParty(Arg.Is<PatchPartyCommand>(patchedCommand =>
            patchedCommand.Notes.HasValue && patchedCommand.Notes.Value == null));
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenNoFieldWasProvided()
    {
        var repository = Substitute.For<IPartyRepository>();
        var command = new PatchPartyCommand(
            "PARTY-001", default, default, default, default, default, default, default, default,
            default, default, default, default, default, default);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler(repository).HandleAsync(command));

        Assert.Equal("At least one field must be provided.", exception.Message);
        repository.DidNotReceive().PatchParty(Arg.Any<PatchPartyCommand>());
    }

    [Fact]
    public async Task HandleAsync_DeduplicatesIdLists()
    {
        var repository = Substitute.For<IPartyRepository>();
        var command = new PatchPartyCommand(
            "PARTY-001", default, default, default, default, default, default, default, default,
            default, default, default,
            new Optional<IReadOnlyCollection<int>>(new[] { 10, 10, 20 }),
            new Optional<IReadOnlyCollection<int>>(new[] { 100, 100 }),
            default);
        repository.PatchParty(Arg.Any<PatchPartyCommand>()).Returns(CreateParty("PARTY-001"));

        await CreateHandler(repository).HandleAsync(command);

        repository.Received(1).PatchParty(Arg.Is<PatchPartyCommand>(patchedCommand =>
            patchedCommand.IndustryIds.Value!.SequenceEqual(new[] { 10, 20 }) &&
            patchedCommand.FeatureIds.Value!.SequenceEqual(new[] { 100 })));
    }

    [Fact]
    public async Task HandleAsync_ForwardsAdditionalFieldsAndNormalizedFlag()
    {
        var repository = Substitute.For<IPartyRepository>();
        var command = new PatchPartyCommand(
            "PARTY-001", default, default, default, default, default, default, default, default,
            default, default, default, default, default, default,
            new Optional<IReadOnlyCollection<AdditionalFieldValueDto>>(
                [new AdditionalFieldValueDto("PoleWlasne1", "Value")]),
            new Optional<IReadOnlyCollection<AdditionalFieldValueDto>>(
                [new AdditionalFieldValueDto("D0", 42m)]),
            new Optional<FlagAssignmentDto?>(new FlagAssignmentDto(12, "  Important  ")));
        repository.PatchParty(Arg.Any<PatchPartyCommand>()).Returns(CreateParty("PARTY-001"));

        await CreateHandler(repository).HandleAsync(command);

        repository.Received(1).PatchParty(Arg.Is<PatchPartyCommand>(patchedCommand =>
            patchedCommand.BasicFields.Value!.Single().Id == "PoleWlasne1" &&
            patchedCommand.AdvancedFields.Value!.Single().Id == "D0" &&
            patchedCommand.Flag.Value == new FlagAssignmentDto(12, "Important")));
    }

    private static PartyDetailsDto CreateParty(string signature) => new(
        1,
        signature,
        "Test party",
        true,
        new PartyClassificationDto(2, "Firma", 7, "Firma", 0, "Standardowy"),
        null,
        null,
        "Test party",
        null,
        null,
        null,
        null,
        null,
        [],
        [],
        null,
        null,
        [],
        [],
        [],
        [],
        new TradeCreditLimitDto(false, null, null, null, null, EmptyDocumentLimit(), EmptyDocumentLimit(), EmptyDocumentLimit()));

    private static DocumentTradeCreditLimitDto EmptyDocumentLimit() => new(false, null, null, null);

    private static PatchPartyHandler CreateHandler(IPartyRepository repository) => new(repository, Substitute.For<IPartyReadModelSynchronizer>());
}
