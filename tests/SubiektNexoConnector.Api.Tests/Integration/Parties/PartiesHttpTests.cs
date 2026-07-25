using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SubiektNexoConnector.Core.Application.Parties.GetPartyDetails;
using SubiektNexoConnector.Core.Application.Parties.PatchParty;
using SubiektNexoConnector.Core.Application.Parties.Shared;

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
    public async Task GetPartyDetails_Returns404_WhenPartyDoesNotExist()
    {
        _factory.Parties.GetDetails(Arg.Any<GetPartyDetailsQuery>()).Returns((PartyDetailsDto?)null);

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
            Addresses: Array.Empty<PartyAddressDto>(),
            Contacts: Array.Empty<PartyContactDto>(),
            TradeCreditLimit: CreateTradeCreditLimit());

        _factory.Parties.GetDetails(Arg.Any<GetPartyDetailsQuery>()).Returns(party);

        var response = await _client.GetAsync($"/parties/{party.Signature}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PartyDetailsDto>();
        body.Should().BeEquivalentTo(party);
    }

    [Fact]
    public async Task PatchParty_Returns200AndJsonBody_WhenPartyWasUpdated()
    {
        _factory.Parties
            .Patch(Arg.Is<PatchPartyCommand>(command =>
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
    public async Task PatchParty_Returns400_WhenNoFieldWasProvided()
    {
        var response = await _client.PatchAsJsonAsync("/parties/PARTY-001", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PatchParty_PassesEmptyLists_WhenIndustriesAndFeaturesAreCleared()
    {
        _factory.Parties
            .Patch(Arg.Is<PatchPartyCommand>(command =>
                command.Industries.HasValue &&
                command.Industries.Value!.Count == 0 &&
                command.Features.HasValue &&
                command.Features.Value!.Count == 0))
            .Returns("PARTY-001");

        var response = await _client.PatchAsJsonAsync(
            "/parties/PARTY-001",
            new
            {
                Industries = Array.Empty<string>(),
                Features = Array.Empty<string>()
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchParty_Returns400ProblemDetails_WhenPartyGroupDoesNotExist()
    {
        _factory.Parties
            .Patch(Arg.Any<PatchPartyCommand>())
            .Returns(_ => throw new InvalidOperationException("Party group 'Missing group' was not found."));

        var response = await _client.PatchAsJsonAsync(
            "/parties/PARTY-001",
            new { PartyGroup = "Missing group" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        body.Should().NotBeNull();
        body!.Status.Should().Be(StatusCodes.Status400BadRequest);
        body.Title.Should().Be("Bad Request");
        body.Detail.Should().Be("Party group 'Missing group' was not found.");
        body.Instance.Should().Be("/parties/PARTY-001");
    }

    [Fact]
    public async Task PatchParty_Returns404_WhenPartyDoesNotExist()
    {
        _factory.Parties.Patch(Arg.Any<PatchPartyCommand>()).Returns((string?)null);

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
