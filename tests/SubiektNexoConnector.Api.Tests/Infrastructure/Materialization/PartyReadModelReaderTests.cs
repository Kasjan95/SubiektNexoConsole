using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Common;
using SubiektNexoConnector.Core.Application.Parties.Addresses.Shared;
using SubiektNexoConnector.Core.Application.Parties.Contacts.Shared;
using SubiektNexoConnector.Core.Application.Parties.GetParties;
using SubiektNexoConnector.Core.Application.Parties.Shared;
using SubiektNexoConnector.Infrastructure.Materialization.Parties;
using SubiektNexoConnector.Infrastructure.Materialization.Persistence;

namespace SubiektNexoConnector.Api.Tests.Infrastructure.Materialization;

public sealed class PartyReadModelReaderTests
{
    [Fact]
    public async Task GetDetailsAsync_ReturnsCompletePartyPersistedByWriter()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var writer = new PartyReadModelWriter(context);
        var reader = new PartyReadModelReader(context);
        var party = CreateCompleteParty();
        var refreshedAtUtc = new DateTime(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc);

        await writer.UpsertAsync(party, CreateDefinitions(), refreshedAtUtc);
        context.ChangeTracker.Clear();

        var result = await reader.GetDetailsAsync(party.Signature);

        Assert.NotNull(result);
        Assert.Equal(refreshedAtUtc, result.RefreshedAtUtc);
        Assert.Equal(party.Id, result.Party.Id);
        Assert.Equal(party.Signature, result.Party.Signature);
        Assert.Equal(party.DisplayName, result.Party.DisplayName);
        Assert.Equal(party.Classification, result.Party.Classification);
        Assert.Equal(party.Flag, result.Party.Flag);
        Assert.Equal(party.BasicFields.ToArray(), result.Party.BasicFields.ToArray());
        Assert.Equal(party.Addresses.ToArray(), result.Party.Addresses.ToArray());
        Assert.Equal(party.Contacts.ToArray(), result.Party.Contacts.ToArray());
        Assert.Equal(party.Industries.ToArray(), result.Party.Industries.ToArray());
        Assert.Equal(party.Features.ToArray(), result.Party.Features.ToArray());
        Assert.Equal(party.TradeCreditLimit, result.Party.TradeCreditLimit);

        foreach (var expectedField in party.AdvancedFields)
        {
            var actualField = Assert.Single(result.Party.AdvancedFields, field => field.Id == expectedField.Id);
            Assert.Equal(expectedField.Value, actualField.Value);
        }
    }

    [Fact]
    public async Task GetAllAsync_AppliesStatusTypeSearchAndPaginationInDatabase()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var writer = new PartyReadModelWriter(context);
        var reader = new PartyReadModelReader(context);
        var refreshedAtUtc = new DateTime(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc);

        await writer.UpsertAsync(CreateListParty(3, "C-3", "Target company", 2, 0), EmptyDefinitions, refreshedAtUtc);
        await writer.UpsertAsync(CreateListParty(1, "C-1", "First company", 2, 0), EmptyDefinitions, refreshedAtUtc);
        await writer.UpsertAsync(CreateListParty(2, "P-2", "Potential company", 2, 2), EmptyDefinitions, refreshedAtUtc);
        await writer.UpsertAsync(CreateListParty(4, "O-4", "Person", 1, 0), EmptyDefinitions, refreshedAtUtc);
        context.ChangeTracker.Clear();

        var secondStandardCompany = await reader.GetAllAsync(new GetPartiesQuery { CustomerStatus = PartyCustomerStatusFilter.Standard, Type = 2, Page = 2, PageSize = 1 });
        var potential = await reader.GetAllAsync(new GetPartiesQuery { CustomerStatus = PartyCustomerStatusFilter.Potential, Page = 1, PageSize = 10 });
        var search = await reader.GetAllAsync(new GetPartiesQuery { CustomerStatus = PartyCustomerStatusFilter.All, Search = "Target", Page = 1, PageSize = 10 });

        Assert.Equal(3, Assert.Single(secondStandardCompany).Id);
        Assert.Equal(2, Assert.Single(potential).Id);
        Assert.Equal(3, Assert.Single(search).Id);
    }

    [Fact]
    public async Task GetDetailsAsync_ReturnsNullWhenPartyDoesNotExist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var reader = new PartyReadModelReader(context);

        var result = await reader.GetDetailsAsync("missing");

        Assert.Null(result);
    }

    private static ReadModelDbContext CreateContext(SqliteConnection connection) => new(new DbContextOptionsBuilder<ReadModelDbContext>().UseSqlite(connection).Options);

    private static PartyDetailsDto CreateCompleteParty() => new(
        1,
        "C-1",
        "Complete company",
        true,
        new PartyClassificationDto(2, "Firma", 7, "Firma", 0, "Standardowy"),
        null,
        null,
        "Complete company sp. z o.o.",
        "1234567890",
        "PL1234567890",
        "123456789",
        "0000123456",
        "Customers",
        ["Retail", "Services"],
        ["VIP", "Local"],
        "Notes",
        new FlagAssignmentDto(3, "Important"),
        [new AdditionalFieldValueDto("PoleWlasne1", "first"), new AdditionalFieldValueDto("PoleWlasne3", null)],
        [new AdditionalFieldValueDto("text", "value"), new AdditionalFieldValueDto("decimal", 12.34m), new AdditionalFieldValueDto("dictionary", 100012)],
        [new PartyAddressDto(10, "First", "1", null, "00-001", "Warszawa", null, null, 1, "Główny", 1)],
        [new PartyContactDto(20, true, 2, "E-mail", "test@example.com", "Main")],
        new TradeCreditLimitDto(true, 14, 30, 3, 5000m, new DocumentTradeCreditLimitDto(true, 1000m, 10m, 20m), new DocumentTradeCreditLimitDto(false, null, null, null), new DocumentTradeCreditLimitDto(true, 2000m, 15m, 25m)));

    private static PartyDetailsDto CreateListParty(int id, string signature, string displayName, short type, int customerStatus) => new(
        id,
        signature,
        displayName,
        true,
        new PartyClassificationDto(type, type == 2 ? "Firma" : "Osoba", type == 2 ? (byte)7 : (byte)3, type == 2 ? "Firma" : "Osoba", customerStatus, customerStatus == 0 ? "Standardowy" : "Potencjalny"),
        type == 1 ? "Jan" : null,
        type == 1 ? "Kowalski" : null,
        type == 2 ? displayName : null,
        $"NIP-{id}",
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

    private static IReadOnlyDictionary<string, AdvancedFieldDefinitionDto> CreateDefinitions()
    {
        var definitions = new[]
        {
            CreateDefinition("text", AdvancedFieldDataType.Text),
            CreateDefinition("decimal", AdvancedFieldDataType.Decimal),
            CreateDefinition("dictionary", AdvancedFieldDataType.Dictionary, "int")
        };
        return definitions.ToDictionary(definition => definition.Id, StringComparer.Ordinal);
    }

    private static AdvancedFieldDefinitionDto CreateDefinition(string id, AdvancedFieldDataType dataType, string? dictionaryKeyType = null) => new(id, id, null, dataType, false, true, true, false, null, null, null, null, dictionaryKeyType is null ? null : new AdvancedFieldDictionaryDto(AdvancedFieldDictionaryKind.Custom, dictionaryKeyType, null, null));

    private static DocumentTradeCreditLimitDto EmptyDocumentLimit() => new(false, null, null, null);

    private static IReadOnlyDictionary<string, AdvancedFieldDefinitionDto> EmptyDefinitions { get; } = new Dictionary<string, AdvancedFieldDefinitionDto>();
}
