using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Api.Controllers;
using SubiektNexoConnector.Core.Application.Parties.GetPartyDetails;
using SubiektNexoConnector.Core.Application.Parties.PatchParty;
using SubiektNexoConnector.Core.Application.Parties.Shared;
using SubiektNexoConnector.Core.Application.Parties.Addresses.Shared;
using SubiektNexoConnector.Core.Application.Parties.Contacts.Shared;

namespace SubiektNexoConnector.Api.Tests.Controllers.Parties;

public class PartiesControllerTests
{
    [Fact]
    public void GetDetails_ReturnsOkWithParty_WhenPartyExists()
    {
        var repository = Substitute.For<IPartyRepository>();
        var party = CreatePartyDetails();
        repository.GetDetailsParty(Arg.Any<GetPartyDetailsQuery>()).Returns(party);

        var controller = new PartiesController();
        var result = controller.GetDetails(party.Signature, new GetPartyDetailsHandler(repository));

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(party, okResult.Value);
        repository.Received(1).GetDetailsParty(
            Arg.Is<GetPartyDetailsQuery>(query => query.PartySignature == party.Signature));
    }

    [Fact]
    public void GetDetails_ReturnsNotFound_WhenPartyDoesNotExist()
    {
        var repository = Substitute.For<IPartyRepository>();
        const string signature = "MISSING";
        repository.GetDetailsParty(Arg.Any<GetPartyDetailsQuery>()).Returns((PartyDetailsDto?)null);

        var controller = new PartiesController();
        var result = controller.GetDetails(signature, new GetPartyDetailsHandler(repository));

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public void Patch_ReturnsUpdatedSignature_WhenPartyExists()
    {
        var repository = Substitute.For<IPartyRepository>();
        var request = new PatchPartyRequestDto(Signature: new("PARTY-002"));
        repository.PatchParty(Arg.Any<PatchPartyCommand>()).Returns("PARTY-002");

        var result = new PartiesController().Patch(
            "PARTY-001",
            request,
            new PatchPartyHandler(repository));

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(new PatchPartyResponseDto("PARTY-002"), okResult.Value);
        repository.Received(1).PatchParty(Arg.Is<PatchPartyCommand>(command =>
            command.PartySignature == "PARTY-001" &&
            command.Signature.HasValue &&
            command.Signature.Value == "PARTY-002"));
    }

    [Fact]
    public void Patch_ReturnsNotFound_WhenPartyDoesNotExist()
    {
        var repository = Substitute.For<IPartyRepository>();
        repository.PatchParty(Arg.Any<PatchPartyCommand>()).Returns((string?)null);

        var result = new PartiesController().Patch(
            "MISSING",
            new PatchPartyRequestDto(Notes: new("Updated note")),
            new PatchPartyHandler(repository));

        Assert.IsType<NotFoundResult>(result.Result);
    }

    private static PartyDetailsDto CreatePartyDetails()
    {
        return new PartyDetailsDto(
            Signature: "PARTY-001",
            DisplayName: "Test party",
            IsActive: true,
            TypeName: "organization",
            SubtypeName: "company",
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
}
