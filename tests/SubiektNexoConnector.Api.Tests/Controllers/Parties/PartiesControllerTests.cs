using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Api.Controllers;
using SubiektNexoConnector.Core.Application.Parties.GetPartyDetails;
using SubiektNexoConnector.Core.Application.Parties.PatchParty;
using SubiektNexoConnector.Core.Application.Parties.Shared;
using SubiektNexoConnector.Core.Application.Parties.Addresses.Shared;
using SubiektNexoConnector.Core.Application.Parties.Contacts.Shared;
using SubiektNexoConnector.Core.Application.Parties;
using SubiektNexoConnector.Core.Application.Parties.CreateParty;
using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Api.Tests.Controllers.Parties;

public class PartiesControllerTests
{
    [Fact]
    public async Task Create_PassesAdvancedFieldsToRepository()
    {
        var repository = Substitute.For<IPartyRepository>();
        var synchronizer = Substitute.For<IPartyReadModelSynchronizer>();
        AdditionalFieldValueDto[] advancedFields = [new("I0", "Required value")];
        var request = new CreatePartyRequestDto(
            "Test party",
            2,
            7,
            "PARTY-001",
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
            [],
            [],
            advancedFields);
        var party = CreatePartyDetails();
        repository.CreateParty(Arg.Any<CreatePartyCommand>()).Returns(party);

        var result = await new PartiesController().Create(
            request,
            new CreatePartyHandler(repository, synchronizer),
            CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(party, createdResult.Value);
        repository.Received(1).CreateParty(Arg.Is<CreatePartyCommand>(command =>
            command.AdvancedFields.SequenceEqual(advancedFields)));
    }

    [Fact]
    public async Task GetDetails_ReturnsOkWithParty_WhenPartyExists()
    {
        var reader = Substitute.For<IPartyReadModelReader>();
        var refresher = Substitute.For<IPartyMaterializationRefresher>();
        var freshnessEvaluator = Substitute.For<IMaterializationFreshnessEvaluator>();
        var party = CreatePartyDetails();
        var refreshedAtUtc = DateTime.UtcNow;
        reader.GetDetailsAsync(party.Signature, Arg.Any<CancellationToken>()).Returns(new MaterializedPartyDetails(party, refreshedAtUtc));
        freshnessEvaluator.EvaluateAsync(MaterializationPolicyDefinitions.Parties, refreshedAtUtc, Arg.Any<CancellationToken>()).Returns(new MaterializationMetadata(MaterializationStatus.Fresh, refreshedAtUtc));

        var controller = CreateController();
        var result = await controller.GetDetails(party.Signature, new GetPartyDetailsHandler(reader, refresher, freshnessEvaluator), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(party, okResult.Value);
        await reader.Received(1).GetDetailsAsync(party.Signature, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetDetails_ReturnsNotFound_WhenPartyDoesNotExist()
    {
        var reader = Substitute.For<IPartyReadModelReader>();
        var refresher = Substitute.For<IPartyMaterializationRefresher>();
        var freshnessEvaluator = Substitute.For<IMaterializationFreshnessEvaluator>();
        const string signature = "MISSING";
        reader.GetDetailsAsync(signature, Arg.Any<CancellationToken>()).Returns((MaterializedPartyDetails?)null);

        var controller = CreateController();
        var result = await controller.GetDetails(signature, new GetPartyDetailsHandler(reader, refresher, freshnessEvaluator), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Patch_ReturnsUpdatedParty_WhenPartyExists()
    {
        var repository = Substitute.For<IPartyRepository>();
        var synchronizer = Substitute.For<IPartyReadModelSynchronizer>();
        var request = new PatchPartyRequestDto(Signature: new("PARTY-002"));
        var party = CreatePartyDetails() with { Signature = "PARTY-002" };
        repository.PatchParty(Arg.Any<PatchPartyCommand>()).Returns(party);

        var result = await new PartiesController().Patch(
            "PARTY-001",
            request,
            new PatchPartyHandler(repository, synchronizer),
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(party, okResult.Value);
        repository.Received(1).PatchParty(Arg.Is<PatchPartyCommand>(command =>
            command.PartySignature == "PARTY-001" &&
            command.Signature.HasValue &&
            command.Signature.Value == "PARTY-002"));
    }

    [Fact]
    public async Task Patch_ReturnsNotFound_WhenPartyDoesNotExist()
    {
        var repository = Substitute.For<IPartyRepository>();
        var synchronizer = Substitute.For<IPartyReadModelSynchronizer>();
        repository.PatchParty(Arg.Any<PatchPartyCommand>()).Returns((PartyDetailsDto?)null);

        var result = await new PartiesController().Patch(
            "MISSING",
            new PatchPartyRequestDto(Notes: new("Updated note")),
            new PatchPartyHandler(repository, synchronizer),
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    private static PartyDetailsDto CreatePartyDetails()
    {
        return new PartyDetailsDto(
            Id: 1,
            Signature: "PARTY-001",
            DisplayName: "Test party",
            IsActive: true,
            Classification: new PartyClassificationDto(2, "Firma", 11, "Firma", 0, "Standardowy"),
            FirstName: null,
            LastName: null,
            CompanyName: "Test party Ltd.",
            TaxId: "1234567890",
            EuTaxId: null,
            BusinessRegistryNumber: null,
            NationalCourtRegisterNumber: null,
            PartyGroup: null,
            Industries: Array.Empty<string>(),
            Features: Array.Empty<string>(),
            Notes: null,
            Flag: null,
            BasicFields: Array.Empty<AdditionalFieldValueDto>(),
            AdvancedFields: Array.Empty<AdditionalFieldValueDto>(),
            Addresses: Array.Empty<PartyAddressDto>(),
            Contacts: Array.Empty<PartyContactDto>(),
            TradeCreditLimit: CreateTradeCreditLimit());
    }

    private static TradeCreditLimitDto CreateTradeCreditLimit() => new(
        IsEnabled: true,
        PaymentTermDays: 7,
        MaximumPaymentDelayDays: 3,
        MaximumUnpaidDocumentCount: 5,
        OverallLimit: 10000m,
        Sales: new DocumentTradeCreditLimitDto(true, 5000m, 100m, 0m),
        GoodsIssue: new DocumentTradeCreditLimitDto(false, null, null, null),
        Order: new DocumentTradeCreditLimitDto(true, 5000m, 100m, 0m));

    private static PartiesController CreateController() => new()
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };
}
