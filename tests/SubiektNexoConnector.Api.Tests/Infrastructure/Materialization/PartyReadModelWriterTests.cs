using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Common;
using SubiektNexoConnector.Core.Application.Parties.Addresses.Shared;
using SubiektNexoConnector.Core.Application.Parties.Contacts.Shared;
using SubiektNexoConnector.Core.Application.Parties.Shared;
using SubiektNexoConnector.Infrastructure.Materialization.Parties;
using SubiektNexoConnector.Infrastructure.Materialization.Persistence;

namespace SubiektNexoConnector.Api.Tests.Infrastructure.Materialization;

public sealed class PartyReadModelWriterTests
{
    [Fact]
    public async Task UpsertAsync_PersistsAndSynchronizesCompleteParty()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var writer = new PartyReadModelWriter(context);
        var definitions = CreateDefinitions();
        var firstRefresh = new DateTime(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc);

        await writer.UpsertAsync(CreateParty(), definitions, firstRefresh);

        context.ChangeTracker.Clear();
        var party = await context.Parties.SingleAsync();
        var basicFields = await context.PartyBasicFields.SingleAsync();
        var advancedFields = await context.PartyAdvancedFields.ToDictionaryAsync(field => field.FieldId);
        var addresses = await context.PartyAddresses.OrderBy(address => address.Position).ToListAsync();
        var contacts = await context.PartyContacts.OrderBy(contact => contact.Position).ToListAsync();

        Assert.Equal(100001, party.Id);
        Assert.Equal("ABC", party.Signature);
        Assert.Equal("Firma", party.TypeName);
        Assert.Equal(5000m, party.OverallCreditLimit);
        Assert.Equal(firstRefresh, party.RefreshedAtUtc);
        Assert.Equal("basic", basicFields.Field1);
        Assert.Equal(0b00000001, basicFields.PresentFieldsMask);
        Assert.Equal("advanced", advancedFields["text"].TextValue);
        Assert.Equal(100012, advancedFields["dictionary"].DictionaryIntegerKey);
        Assert.Equal([10, 20], addresses.Select(address => address.AddressId));
        Assert.Equal([30, 40], contacts.Select(contact => contact.ContactId));
        Assert.Equal(["Retail", "Services"], await context.PartyIndustries.OrderBy(industry => industry.Position).Select(industry => industry.Name).ToListAsync());
        Assert.Equal(["VIP", "Local"], await context.PartyFeatures.OrderBy(feature => feature.Position).Select(feature => feature.Name).ToListAsync());

        var secondRefresh = firstRefresh.AddMinutes(5);
        var updated = CreateParty() with
        {
            Signature = "ABC-UPDATED",
            DisplayName = "Updated party",
            BasicFields = [new("PoleWlasne2", "new basic")],
            AdvancedFields = [new("dictionary", 100013)],
            Addresses = [new(20, "Second", "2", null, "00-002", "Kraków", null, null, 1, "Główny", 1)],
            Contacts = [new(40, true, 2, "E-mail", "updated@example.com", null)],
            Industries = ["Manufacturing"],
            Features = ["Strategic"]
        };

        await writer.UpsertAsync(updated, definitions, secondRefresh);
        context.ChangeTracker.Clear();

        party = await context.Parties.SingleAsync();
        basicFields = await context.PartyBasicFields.SingleAsync();
        advancedFields = await context.PartyAdvancedFields.ToDictionaryAsync(field => field.FieldId);

        Assert.Equal("ABC-UPDATED", party.Signature);
        Assert.Equal(secondRefresh, party.RefreshedAtUtc);
        Assert.Null(basicFields.Field1);
        Assert.Equal("new basic", basicFields.Field2);
        Assert.Equal(0b00000010, basicFields.PresentFieldsMask);
        Assert.Single(advancedFields);
        Assert.Equal(100013, advancedFields["dictionary"].DictionaryIntegerKey);
        Assert.Equal(20, Assert.Single(await context.PartyAddresses.ToListAsync()).AddressId);
        Assert.Equal("updated@example.com", Assert.Single(await context.PartyContacts.ToListAsync()).ContactValue);
        Assert.Equal("Manufacturing", Assert.Single(await context.PartyIndustries.ToListAsync()).Name);
        Assert.Equal("Strategic", Assert.Single(await context.PartyFeatures.ToListAsync()).Name);
    }

    [Fact]
    public async Task UpsertBatchAsync_PersistsAllParties()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var writer = new PartyReadModelWriter(context);
        var first = CreateParty();
        var second = first with { Id = 100002, Signature = "XYZ", DisplayName = "Second party", Addresses = [], Contacts = [] };
        var refreshedAtUtc = new DateTime(2026, 10, 13, 9, 0, 0, DateTimeKind.Utc);

        await writer.UpsertBatchAsync([first, second], CreateDefinitions(), refreshedAtUtc);

        Assert.Equal(2, await context.Parties.CountAsync());
        Assert.Equal(2, await context.PartyBasicFields.CountAsync());
        Assert.Equal(4, await context.PartyAdvancedFields.CountAsync());
        Assert.All(await context.Parties.AsNoTracking().ToListAsync(), party => Assert.Equal(refreshedAtUtc, party.RefreshedAtUtc));
    }

    [Fact]
    public async Task DeleteNotRefreshedSinceAsync_RemovesOnlyPartiesMissingFromCompletedCycle()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var writer = new PartyReadModelWriter(context);
        var cycleStartedAtUtc = new DateTime(2026, 10, 13, 9, 0, 0, DateTimeKind.Utc);
        var current = CreateParty();
        var missing = current with { Id = 100002, Signature = "MISSING", DisplayName = "Removed in Nexo" };
        await writer.UpsertAsync(current, CreateDefinitions(), cycleStartedAtUtc.AddMinutes(1));
        await writer.UpsertAsync(missing, CreateDefinitions(), cycleStartedAtUtc.AddMinutes(-1));
        context.ChangeTracker.Clear();

        var removedCount = await writer.DeleteNotRefreshedSinceAsync(cycleStartedAtUtc);

        Assert.Equal(1, removedCount);
        Assert.Equal(current.Id, (await context.Parties.SingleAsync()).Id);
        Assert.DoesNotContain(await context.PartyAddresses.ToListAsync(), address => address.PartyId == missing.Id);
    }

    private static ReadModelDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ReadModelDbContext>().UseSqlite(connection).Options;
        return new ReadModelDbContext(options);
    }

    private static PartyDetailsDto CreateParty() => new(
        100001,
        "ABC",
        "Example party",
        true,
        new PartyClassificationDto(2, "Firma", 7, "Firma", 0, "Standardowy"),
        null,
        null,
        "Example sp. z o.o.",
        "1234567890",
        null,
        "123456789",
        "0000123456",
        "Customers",
        ["Retail", "Services"],
        ["VIP", "Local"],
        "Notes",
        new FlagAssignmentDto(3, "Important"),
        [new("PoleWlasne1", "basic")],
        [new("text", "advanced"), new("dictionary", 100012)],
        [
            new PartyAddressDto(10, "First", "1", null, "00-001", "Warszawa", null, null, 1, "Główny", 1),
            new PartyAddressDto(20, "Second", "2", null, "00-002", "Kraków", null, null, 1, "Korespondencyjny", 2)
        ],
        [
            new PartyContactDto(30, false, 1, "Telefon", "123456789", null),
            new PartyContactDto(40, true, 2, "E-mail", "test@example.com", "Main")
        ],
        new TradeCreditLimitDto(
            true,
            14,
            30,
            3,
            5000m,
            new DocumentTradeCreditLimitDto(true, 1000m, 10m, 20m),
            new DocumentTradeCreditLimitDto(false, null, null, null),
            new DocumentTradeCreditLimitDto(true, 2000m, 15m, 25m)));

    private static IReadOnlyDictionary<string, AdvancedFieldDefinitionDto> CreateDefinitions()
    {
        var definitions = new[]
        {
            CreateDefinition("text", AdvancedFieldDataType.Text),
            CreateDefinition("dictionary", AdvancedFieldDataType.Dictionary, "int")
        };
        return definitions.ToDictionary(definition => definition.Id, StringComparer.Ordinal);
    }

    private static AdvancedFieldDefinitionDto CreateDefinition(string id, AdvancedFieldDataType dataType, string? dictionaryKeyType = null) => new(
        id,
        id,
        null,
        dataType,
        false,
        true,
        true,
        false,
        null,
        null,
        null,
        null,
        dictionaryKeyType is null ? null : new AdvancedFieldDictionaryDto(AdvancedFieldDictionaryKind.Custom, dictionaryKeyType, null, null));
}
