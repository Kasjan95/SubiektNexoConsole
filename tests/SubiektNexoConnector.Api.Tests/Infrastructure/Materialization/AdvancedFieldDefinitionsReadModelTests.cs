using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Infrastructure.Materialization.AdditionalFields;
using SubiektNexoConnector.Infrastructure.Materialization.Persistence;

namespace SubiektNexoConnector.Api.Tests.Infrastructure.Materialization;

public sealed class AdvancedFieldDefinitionsReadModelTests
{
    [Fact]
    public async Task ReplaceAndGetAsync_PreservesCompleteSnapshot()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var writer = new AdvancedFieldDefinitionsReadModelWriter(context);
        var reader = new AdvancedFieldDefinitionsReadModelReader(context);
        var dictionaryDefault = Guid.Parse("81364a88-9652-4a51-a084-579092b9b2d6");
        var expected = new AdvancedFieldDefinitionsDto(
            AdditionalFieldTarget.Product,
            [
                new AdvancedFieldGroupDto(
                    "Logistics",
                    1,
                    [
                        CreateDefinition("I0", AdvancedFieldDataType.Decimal, 12.34m),
                        CreateDefinition(
                            "I1",
                            AdvancedFieldDataType.Dictionary,
                            100012,
                            new AdvancedFieldDictionaryDto(
                                AdvancedFieldDictionaryKind.Custom,
                                "int",
                                null,
                                [
                                    new AdvancedFieldDictionaryOptionDto("100012", "First", true),
                                    new AdvancedFieldDictionaryOptionDto("100013", "Second", false)
                                ]))
                    ]),
                new AdvancedFieldGroupDto("Empty group", 2, [])
            ],
            [
                CreateDefinition("I2", AdvancedFieldDataType.Text, "Default text"),
                CreateDefinition(
                    "I3",
                    AdvancedFieldDataType.Dictionary,
                    dictionaryDefault,
                    new AdvancedFieldDictionaryDto(
                        AdvancedFieldDictionaryKind.System,
                        "guid",
                        "currencies",
                        null)),
                CreateDefinition(
                    "I4",
                    AdvancedFieldDataType.Dictionary,
                    null,
                    new AdvancedFieldDictionaryDto(
                        AdvancedFieldDictionaryKind.Custom,
                        "int",
                        null,
                        []))
            ]);

        await writer.ReplaceAsync(expected);
        var actual = await reader.GetAsync(AdditionalFieldTarget.Product);

        actual.Should().BeEquivalentTo(expected, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task ReplaceAsync_ReplacesOnlySelectedTarget()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var writer = new AdvancedFieldDefinitionsReadModelWriter(context);
        var reader = new AdvancedFieldDefinitionsReadModelReader(context);
        var partySnapshot = CreateSnapshot(AdditionalFieldTarget.Party, CreateDefinition("party-field", AdvancedFieldDataType.Text, "Party"));

        await writer.ReplaceAsync(CreateSnapshot(AdditionalFieldTarget.Product, CreateDefinition("old-product-field", AdvancedFieldDataType.Integer, 1)));
        await writer.ReplaceAsync(partySnapshot);
        await writer.ReplaceAsync(CreateSnapshot(AdditionalFieldTarget.Product, CreateDefinition("new-product-field", AdvancedFieldDataType.Boolean, true)));

        var product = await reader.GetAsync(AdditionalFieldTarget.Product);
        var party = await reader.GetAsync(AdditionalFieldTarget.Party);

        product.Fields.Should().ContainSingle(field => field.Id == "new-product-field");
        product.Fields.Should().NotContain(field => field.Id == "old-product-field");
        party.Should().BeEquivalentTo(partySnapshot, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task ReplaceAsync_RollsBackAndPreservesPreviousSnapshot_WhenNewSnapshotIsInvalid()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var writer = new AdvancedFieldDefinitionsReadModelWriter(context);
        var reader = new AdvancedFieldDefinitionsReadModelReader(context);
        var original = CreateSnapshot(AdditionalFieldTarget.Product, CreateDefinition("original", AdvancedFieldDataType.Text, "value"));
        var duplicateField = CreateDefinition("duplicate", AdvancedFieldDataType.Text, null);
        var invalid = new AdvancedFieldDefinitionsDto(AdditionalFieldTarget.Product, [], [duplicateField, duplicateField]);
        await writer.ReplaceAsync(original);

        await Assert.ThrowsAsync<DbUpdateException>(() => writer.ReplaceAsync(invalid));
        var actual = await reader.GetAsync(AdditionalFieldTarget.Product);

        actual.Should().BeEquivalentTo(original, options => options.WithStrictOrdering());
    }

    private static ReadModelDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ReadModelDbContext>()
            .UseSqlite(connection)
            .Options;
        return new ReadModelDbContext(options);
    }

    private static AdvancedFieldDefinitionsDto CreateSnapshot(AdditionalFieldTarget target, AdvancedFieldDefinitionDto definition) => new(target, [], [definition]);

    private static AdvancedFieldDefinitionDto CreateDefinition(string id, AdvancedFieldDataType dataType, object? defaultValue, AdvancedFieldDictionaryDto? dictionary = null) => new(
        id,
        $"Name {id}",
        $"Description {id}",
        dataType,
        true,
        true,
        false,
        true,
        dataType == AdvancedFieldDataType.Decimal ? 2 : null,
        1,
        5,
        defaultValue,
        dictionary);
}
