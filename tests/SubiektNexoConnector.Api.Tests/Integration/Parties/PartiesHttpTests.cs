using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Parties.Addresses.Shared;
using SubiektNexoConnector.Core.Application.Parties.CreateParty;
using SubiektNexoConnector.Core.Application.Parties.Addresses.CreateAddress;
using SubiektNexoConnector.Core.Application.Parties.Addresses.PatchAddress;
using SubiektNexoConnector.Core.Application.Parties.Addresses.DeleteAddress;
using SubiektNexoConnector.Core.Application.Parties.Contacts.CreateContact;
using SubiektNexoConnector.Core.Application.Parties.Contacts.DeleteContact;
using SubiektNexoConnector.Core.Application.Parties.Contacts.PatchContact;
using SubiektNexoConnector.Core.Application.Parties.GetParties;
using SubiektNexoConnector.Core.Application.Parties.GetPartyDetails;
using SubiektNexoConnector.Core.Application.Parties.PatchParty;
using SubiektNexoConnector.Core.Application.Parties.Shared;
using SubiektNexoConnector.Core.Application.Parties.Contacts.Shared;

namespace SubiektNexoConnector.Api.Tests.Integration;

public class PartiesHttpTests : IClassFixture<TestApiFactory>
{
    private readonly HttpClient _client;
    private readonly TestApiFactory _factory;

    public PartiesHttpTests(TestApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateBusinessClient();
    }

    [Fact]
    public async Task GetParties_Returns400ValidationProblem_WhenPageIsInvalid()
    {
        _factory.Parties.ClearReceivedCalls();

        var response = await _client.GetAsync("/parties?page=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        body!.Errors.Should().ContainKey("page");
        _factory.Parties.DidNotReceive().GetAllParties(Arg.Any<GetPartiesQuery>());
    }

    [Fact]
    public async Task GetPartyCreateOptions_Returns200AndJsonBody()
    {
        var options = new PartyCreateOptionsDto(
            PartyTypes: [new PartyTypeOptionDto(1, 3, "Person")],
            AddressTypes: [new ReferenceDataOptionDto(1, "Main")],
            ContactTypes: [new ReferenceDataOptionDto(3, "Email")],
            Countries: [new CountryOptionDto(1, "Poland", "PL")],
            PartyGroups: [new ReferenceDataOptionDto(100000, "VIP")],
            Industries: [new ReferenceDataOptionDto(1, "Retail")],
            Features: [new ReferenceDataOptionDto(100001, "B2B")]);
        _factory.Parties.GetCreateOptions().Returns(options);

        var response = await _client.GetAsync("/parties/create-options");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PartyCreateOptionsDto>();
        body.Should().BeEquivalentTo(options);
    }

    [Fact]
    public async Task GetPartyDetails_Returns404_WhenPartyDoesNotExist()
    {
        _factory.Parties.GetDetailsParty(Arg.Any<GetPartyDetailsQuery>()).Returns((PartyDetailsDto?)null);

        var response = await _client.GetAsync("/parties/MISSING");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPartyDetails_Returns200AndJsonBody()
    {
        var party = new PartyDetailsDto(
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

        _factory.Parties.GetDetailsParty(Arg.Any<GetPartyDetailsQuery>()).Returns(party);

        var response = await _client.GetAsync($"/parties/{party.Signature}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PartyDetailsDto>();
        body.Should().BeEquivalentTo(party);
    }

    [Fact]
    public async Task PatchParty_Returns200AndJsonBody_WhenPartyWasUpdated()
    {
        _factory.Parties
            .PatchParty(Arg.Is<PatchPartyCommand>(command =>
                command.PartySignature == "PARTY-001" &&
                command.Signature.HasValue &&
                command.Signature.Value == "PARTY-002" &&
                command.Notes.HasValue &&
                command.Notes.Value == null))
            .Returns("PARTY-002");

        var response = await _client.PatchAsJsonAsync(
            "/parties/PARTY-001",
            new
            {
                Signature = "PARTY-002",
                Notes = (string?)null
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PatchPartyResponseDto>();
        body.Should().BeEquivalentTo(new PatchPartyResponseDto("PARTY-002"));
    }

    [Fact]
    public async Task CreatePartyAddress_Returns201WithCreatedAddress()
    {
        var address = new PartyAddressDto(
            AddressId: 123,
            Street: "Marszałkowska",
            HouseNumber: "10",
            ApartmentNumber: null,
            PostalCode: "00-590",
            City: "Warszawa",
            Municipality: null,
            Voivodeship: null,
            CountryId: 1,
            AddressTypeName: "Korespondencyjny",
            AddressTypeId: 2);
        _factory.Parties
            .CreatePartyAddress(Arg.Any<CreatePartyAddressCommand>())
            .Returns(new CreatePartyAddressResult(address, IsCreated: true));

        var response = await _client.PostAsJsonAsync(
            "/parties/PARTY-001/addresses",
            new
            {
                AddressTypeId = 2,
                Street = "Marszałkowska",
                HouseNumber = "10",
                City = "Warszawa"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<PartyAddressDto>();
        body.Should().BeEquivalentTo(address);
        _factory.Parties.Received(1).CreatePartyAddress(Arg.Is<CreatePartyAddressCommand>(command =>
            command.PartySignature == "PARTY-001" &&
            command.Address.AddressTypeId == 2));
    }

    [Fact]
    public async Task CreatePartyAddress_Returns200WhenPrimaryAddressWasUpdated()
    {
        var address = new PartyAddressDto(
            AddressId: 1,
            Street: "Nowa",
            HouseNumber: "1",
            ApartmentNumber: null,
            PostalCode: "00-001",
            City: "Warszawa",
            Municipality: null,
            Voivodeship: null,
            CountryId: 1,
            AddressTypeName: "Główny",
            AddressTypeId: 1);
        _factory.Parties
            .CreatePartyAddress(Arg.Any<CreatePartyAddressCommand>())
            .Returns(new CreatePartyAddressResult(address, IsCreated: false));

        var response = await _client.PostAsJsonAsync(
            "/parties/PARTY-001/addresses",
            new
            {
                AddressTypeId = 1,
                Street = "Nowa",
                HouseNumber = "1",
                City = "Warszawa"
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PartyAddressDto>();
        body.Should().BeEquivalentTo(address);
    }

    [Fact]
    public async Task CreatePartyAddress_Returns404_WhenPartyDoesNotExist()
    {
        _factory.Parties.CreatePartyAddress(Arg.Any<CreatePartyAddressCommand>()).Returns((CreatePartyAddressResult?)null);

        var response = await _client.PostAsJsonAsync(
            "/parties/MISSING/addresses",
            new { AddressTypeId = 2, Street = "Marszałkowska", City = "Warszawa" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreatePartyAddress_Returns400_WhenAddressTypeIdIsNotPositive()
    {
        _factory.Parties.ClearReceivedCalls();

        var response = await _client.PostAsJsonAsync(
            "/parties/PARTY-001/addresses",
            new { AddressTypeId = 0, Street = "Marszałkowska", City = "Warszawa" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _factory.Parties.DidNotReceive().CreatePartyAddress(Arg.Any<CreatePartyAddressCommand>());
    }

    [Fact]
    public async Task PatchPartyAddress_ForwardsPostalCodeAndCountryId()
    {
        var address = new PartyAddressDto(
            AddressId: 123,
            Street: "Nowa",
            HouseNumber: "10",
            ApartmentNumber: "2",
            PostalCode: "00-001",
            City: "Warszawa",
            Municipality: null,
            Voivodeship: null,
            CountryId: 1,
            AddressTypeName: "Główny",
            AddressTypeId: 1);
        _factory.Parties
            .PatchPartyAddress(Arg.Is<PatchPartyAddressCommand>(command =>
                command.PartySignature == "PARTY-001" &&
                command.AddressId == 123 &&
                command.PostalCode.HasValue &&
                command.PostalCode.Value == "00-001" &&
                command.CountryId.HasValue &&
                command.CountryId.Value == 1))
            .Returns(address);

        var response = await _client.PatchAsJsonAsync(
            "/parties/PARTY-001/addresses/123",
            new
            {
                PostalCode = "00-001",
                CountryId = 1
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PartyAddressDto>();
        body.Should().BeEquivalentTo(address);
    }

    [Fact]
    public async Task PatchPartyAddress_Returns404_WhenAddressDoesNotExist()
    {
        _factory.Parties.PatchPartyAddress(Arg.Any<PatchPartyAddressCommand>()).Returns((PartyAddressDto?)null);

        var response = await _client.PatchAsJsonAsync(
            "/parties/PARTY-001/addresses/999",
            new { City = "Warsaw" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PatchPartyAddress_Returns400_WhenNoFieldWasProvided()
    {
        var response = await _client.PatchAsJsonAsync("/parties/PARTY-001/addresses/123", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeletePartyAddress_Returns204_WhenAddressWasDeleted()
    {
        _factory.Parties.DeletePartyAddress(Arg.Is<DeletePartyAddressCommand>(command =>
                command.PartySignature == "PARTY-001" && command.AddressId == 123))
            .Returns(DeletePartyResourceResult.Deleted);

        var response = await _client.DeleteAsync("/parties/PARTY-001/addresses/123");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeletePartyAddress_Returns404_WhenAddressDoesNotExist()
    {
        _factory.Parties.DeletePartyAddress(Arg.Any<DeletePartyAddressCommand>())
            .Returns(DeletePartyResourceResult.NotFound);

        var response = await _client.DeleteAsync("/parties/PARTY-001/addresses/999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletePartyAddress_Returns400ProblemDetails_WhenNexoRejectsDeletion()
    {
        const string errorMessage = "Party address deletion failed:\nAdresPodstawowy: A primary address is required.";
        _factory.Parties.DeletePartyAddress(Arg.Any<DeletePartyAddressCommand>())
            .Returns(_ => throw new InvalidOperationException(errorMessage));

        var response = await _client.DeleteAsync("/parties/PARTY-001/addresses/123");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        body.Should().NotBeNull();
        body!.Detail.Should().Be(errorMessage);
        body.Instance.Should().Be("/parties/PARTY-001/addresses/123");
    }

    [Fact]
    public async Task CreatePartyContact_Returns201AndForwardsValidatedInput()
    {
        var contact = new PartyContactDto(123, true, 3, "E-mail", "contact@example.com", "Sales");
        _factory.Parties
            .CreatePartyContact(Arg.Is<CreatePartyContactCommand>(command =>
                command.PartySignature == "PARTY-001" &&
                command.Contact.ContactTypeId == 3 &&
                command.Contact.Value == "contact@example.com" &&
                command.Contact.IsPrimary &&
                command.Contact.Comment == "Sales"))
            .Returns(contact);

        var response = await _client.PostAsJsonAsync(
            "/parties/PARTY-001/contacts",
            new { ContactTypeId = 3, Value = "contact@example.com", IsPrimary = true, Comment = "Sales" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<PartyContactDto>()).Should().BeEquivalentTo(contact);
    }

    [Fact]
    public async Task CreatePartyContact_Returns404_WhenPartyDoesNotExist()
    {
        _factory.Parties.CreatePartyContact(Arg.Any<CreatePartyContactCommand>()).Returns((PartyContactDto?)null);

        var response = await _client.PostAsJsonAsync(
            "/parties/MISSING/contacts",
            new { ContactTypeId = 3, Value = "contact@example.com", IsPrimary = true });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreatePartyContact_Returns400_WhenContactTypeIdIsNotPositive()
    {
        _factory.Parties.ClearReceivedCalls();

        var response = await _client.PostAsJsonAsync(
            "/parties/PARTY-001/contacts",
            new { ContactTypeId = 0, Value = "contact@example.com", IsPrimary = true });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _factory.Parties.DidNotReceive().CreatePartyContact(Arg.Any<CreatePartyContactCommand>());
    }

    [Fact]
    public async Task PatchPartyContact_Returns200AndNormalizesTextFields()
    {
        var contact = new PartyContactDto(123, false, 3, "E-mail", "contact@example.com", null);
        _factory.Parties
            .PatchPartyContact(Arg.Is<PatchPartyContactCommand>(command =>
                command.PartySignature == "PARTY-001" &&
                command.ContactId == 123 &&
                command.IsPrimary.HasValue && !command.IsPrimary.Value &&
                command.ContactValue.HasValue && command.ContactValue.Value == "contact@example.com" &&
                command.ContactDescription.HasValue && command.ContactDescription.Value == null))
            .Returns(contact);

        var response = await _client.PatchAsJsonAsync(
            "/parties/PARTY-001/contacts/123",
            new { IsPrimary = false, ContactValue = "  contact@example.com  ", ContactDescription = "  " });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<PartyContactDto>()).Should().BeEquivalentTo(contact);
    }

    [Fact]
    public async Task PatchPartyContact_Returns400_WhenNoFieldWasProvided()
    {
        _factory.Parties.ClearReceivedCalls();

        var response = await _client.PatchAsJsonAsync("/parties/PARTY-001/contacts/123", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _factory.Parties.DidNotReceive().PatchPartyContact(Arg.Any<PatchPartyContactCommand>());
    }

    [Fact]
    public async Task PatchPartyContact_Returns404_WhenContactDoesNotExist()
    {
        _factory.Parties.PatchPartyContact(Arg.Any<PatchPartyContactCommand>()).Returns((PartyContactDto?)null);

        var response = await _client.PatchAsJsonAsync(
            "/parties/PARTY-001/contacts/999",
            new { ContactValue = "contact@example.com" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletePartyContact_UsesContactIdRouteParameterAndReturns204()
    {
        _factory.Parties
            .DeletePartyContact(Arg.Is<DeletePartyContactCommand>(command =>
                command.PartySignature == "PARTY-001" && command.ContactId == 123))
            .Returns(DeletePartyResourceResult.Deleted);

        var response = await _client.DeleteAsync("/parties/PARTY-001/contacts/123");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PatchParty_Returns400_WhenNoFieldWasProvided()
    {
        var response = await _client.PatchAsJsonAsync("/parties/PARTY-001", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PatchParty_PassesEmptyLists_WhenIndustryIdsAndFeatureIdsAreCleared()
    {
        _factory.Parties
            .PatchParty(Arg.Is<PatchPartyCommand>(command =>
                command.IndustryIds.HasValue &&
                command.IndustryIds.Value!.Count == 0 &&
                command.FeatureIds.HasValue &&
                command.FeatureIds.Value!.Count == 0))
            .Returns("PARTY-001");

        var response = await _client.PatchAsJsonAsync(
            "/parties/PARTY-001",
            new
            {
                IndustryIds = Array.Empty<int>(),
                FeatureIds = Array.Empty<int>()
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchParty_ForwardsAdditionalFieldsAndFlag()
    {
        _factory.Parties
            .PatchParty(Arg.Is<PatchPartyCommand>(command =>
                command.BasicFields.HasValue &&
                command.BasicFields.Value!.Single().Id == "PoleWlasne1" &&
                command.AdvancedFields.HasValue &&
                command.AdvancedFields.Value!.Single().Id == "D0" &&
                command.Flag.HasValue &&
                command.Flag.Value == new SubiektNexoConnector.Core.Application.Common.FlagAssignmentDto(2, "Important")))
            .Returns("PARTY-001");

        var response = await _client.PatchAsJsonAsync(
            "/parties/PARTY-001",
            new
            {
                BasicFields = new[] { new { Id = "PoleWlasne1", Value = "Value" } },
                AdvancedFields = new[] { new { Id = "D0", Value = 42 } },
                Flag = new { Id = 2, Comment = "  Important  " }
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchParty_Returns400ProblemDetails_WhenPartyGroupDoesNotExist()
    {
        _factory.Parties
            .PatchParty(Arg.Any<PatchPartyCommand>())
            .Returns(_ => throw new InvalidOperationException("Party group '999999' was not found."));

        var response = await _client.PatchAsJsonAsync(
            "/parties/PARTY-001",
            new { PartyGroupId = 999999 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        body.Should().NotBeNull();
        body!.Status.Should().Be(StatusCodes.Status400BadRequest);
        body.Title.Should().Be("Bad Request");
        body.Detail.Should().Be("Party group '999999' was not found.");
        body.Instance.Should().Be("/parties/PARTY-001");
    }

    [Fact]
    public async Task PatchParty_Returns404_WhenPartyDoesNotExist()
    {
        _factory.Parties.PatchParty(Arg.Any<PatchPartyCommand>()).Returns((string?)null);

        var response = await _client.PatchAsJsonAsync(
            "/parties/MISSING",
            new { Notes = "Updated note" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
