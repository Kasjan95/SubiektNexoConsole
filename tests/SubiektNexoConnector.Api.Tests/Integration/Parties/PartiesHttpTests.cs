using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using SubiektNexoConnector.Core.Application.Parties.GetPartyDetails;
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
            Industry: null,
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
