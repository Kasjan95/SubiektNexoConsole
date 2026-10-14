using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Parties;
using SubiektNexoConnector.Core.Application.Parties.Shared;
using SubiektNexoConnector.Infrastructure.Materialization.Persistence;

namespace SubiektNexoConnector.Infrastructure.Materialization.Parties;

internal sealed class PartyReadModelWriter : IPartyReadModelWriter
{
    private readonly ReadModelDbContext _dbContext;
    private readonly ILogger<PartyReadModelWriter> _logger;

    public PartyReadModelWriter(ReadModelDbContext dbContext, ILogger<PartyReadModelWriter>? logger = null)
    {
        _dbContext = dbContext;
        _logger = logger ?? NullLogger<PartyReadModelWriter>.Instance;
    }

    public async Task UpsertAsync(PartyDetailsDto party, IReadOnlyDictionary<string, AdvancedFieldDefinitionDto> advancedFieldDefinitions, DateTime refreshedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(advancedFieldDefinitions);

        var entity = await CreateLoadedQuery().SingleOrDefaultAsync(entity => entity.Id == party.Id, cancellationToken);
        UpsertCore(party, entity, advancedFieldDefinitions, refreshedAtUtc);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpsertBatchAsync(IReadOnlyCollection<PartyDetailsDto> parties, IReadOnlyDictionary<string, AdvancedFieldDefinitionDto> advancedFieldDefinitions, DateTime refreshedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parties);
        ArgumentNullException.ThrowIfNull(advancedFieldDefinitions);

        if (parties.Count == 0)
            return;

        var duplicateId = parties.GroupBy(party => party.Id).FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateId is not null)
            throw new InvalidOperationException($"Party batch contains duplicate Nexo id '{duplicateId}'.");

        var stopwatch = Stopwatch.StartNew();
        var partyIds = parties.Select(party => party.Id).ToArray();

        try
        {
            var existing = await CreateLoadedQuery().Where(entity => partyIds.Contains(entity.Id)).ToDictionaryAsync(entity => entity.Id, cancellationToken);

            foreach (var party in parties)
            {
                cancellationToken.ThrowIfCancellationRequested();
                existing.TryGetValue(party.Id, out var entity);
                UpsertCore(party, entity, advancedFieldDefinitions, refreshedAtUtc);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _dbContext.ChangeTracker.Clear();
        }

        stopwatch.Stop();
        var partiesPerSecond = parties.Count / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001);
        _logger.LogInformation("Persisted party materialization batch in SQLite. Parties: {PartyCount}; duration: {DurationMs} ms; rate: {PartiesPerSecond} parties/s.", parties.Count, stopwatch.Elapsed.TotalMilliseconds, partiesPerSecond);
    }

    public async Task DeleteAsync(string signature, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signature);

        var party = await _dbContext.Parties.SingleOrDefaultAsync(party => party.Signature == signature, cancellationToken);
        if (party is null)
            return;

        _dbContext.Parties.Remove(party);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<int> DeleteNotRefreshedSinceAsync(DateTime cycleStartedAtUtc, CancellationToken cancellationToken = default) => _dbContext.Parties.Where(party => party.RefreshedAtUtc < cycleStartedAtUtc).ExecuteDeleteAsync(cancellationToken);

    private IQueryable<PartyEntity> CreateLoadedQuery() => _dbContext.Parties
        .Include(entity => entity.BasicFields)
        .Include(entity => entity.AdvancedFields)
        .Include(entity => entity.Addresses)
        .Include(entity => entity.Contacts)
        .Include(entity => entity.Industries)
        .Include(entity => entity.Features)
        .AsSplitQuery();

    private void UpsertCore(PartyDetailsDto party, PartyEntity? entity, IReadOnlyDictionary<string, AdvancedFieldDefinitionDto> advancedFieldDefinitions, DateTime refreshedAtUtc)
    {
        if (entity is null)
        {
            entity = new PartyEntity
            {
                Id = party.Id,
                Signature = party.Signature,
                DisplayName = party.DisplayName,
                TypeName = party.Classification.TypeName,
                SubtypeName = party.Classification.SubtypeName,
                CustomerStatusName = party.Classification.CustomerStatusName
            };
            _dbContext.Parties.Add(entity);
        }

        MapParty(entity, party, refreshedAtUtc);
        UpsertBasicFields(entity, party, refreshedAtUtc);
        SynchronizeAdvancedFields(entity, party, advancedFieldDefinitions, refreshedAtUtc);
        SynchronizeAddresses(entity, party, refreshedAtUtc);
        SynchronizeContacts(entity, party, refreshedAtUtc);
        SynchronizeIndustries(entity, party.Industries, refreshedAtUtc);
        SynchronizeFeatures(entity, party.Features, refreshedAtUtc);
    }

    private static void MapParty(PartyEntity target, PartyDetailsDto source, DateTime refreshedAtUtc)
    {
        target.Signature = source.Signature;
        target.DisplayName = source.DisplayName;
        target.IsActive = source.IsActive;
        target.Type = source.Classification.Type;
        target.TypeName = source.Classification.TypeName;
        target.Subtype = source.Classification.Subtype;
        target.SubtypeName = source.Classification.SubtypeName;
        target.CustomerStatus = source.Classification.CustomerStatus;
        target.CustomerStatusName = source.Classification.CustomerStatusName;
        target.FirstName = source.FirstName;
        target.LastName = source.LastName;
        target.CompanyName = source.CompanyName;
        target.TaxId = source.TaxId;
        target.EuTaxId = source.EuTaxId;
        target.BusinessRegistryNumber = source.BusinessRegistryNumber;
        target.NationalCourtRegisterNumber = source.NationalCourtRegisterNumber;
        target.PartyGroup = source.PartyGroup;
        target.Notes = source.Notes;
        target.FlagId = source.Flag?.Id;
        target.FlagComment = source.Flag?.Comment;
        target.TradeCreditEnabled = source.TradeCreditLimit.IsEnabled;
        target.PaymentTermDays = source.TradeCreditLimit.PaymentTermDays;
        target.MaximumPaymentDelayDays = source.TradeCreditLimit.MaximumPaymentDelayDays;
        target.MaximumUnpaidDocumentCount = source.TradeCreditLimit.MaximumUnpaidDocumentCount;
        target.OverallCreditLimit = source.TradeCreditLimit.OverallLimit;
        MapTradeCredit(target, source.TradeCreditLimit);
        target.RefreshedAtUtc = refreshedAtUtc;
    }

    private static void MapTradeCredit(PartyEntity target, TradeCreditLimitDto source)
    {
        target.SalesCreditLimitEnabled = source.Sales.IsMaximumAmountEnabled;
        target.SalesMaximumAmount = source.Sales.MaximumAmount;
        target.SalesBelowMaximumAmountPercent = source.Sales.CreditLimitBelowMaximumAmountPercent;
        target.SalesAboveMaximumAmountPercent = source.Sales.CreditLimitAboveMaximumAmountPercent;
        target.GoodsIssueCreditLimitEnabled = source.GoodsIssue.IsMaximumAmountEnabled;
        target.GoodsIssueMaximumAmount = source.GoodsIssue.MaximumAmount;
        target.GoodsIssueBelowMaximumAmountPercent = source.GoodsIssue.CreditLimitBelowMaximumAmountPercent;
        target.GoodsIssueAboveMaximumAmountPercent = source.GoodsIssue.CreditLimitAboveMaximumAmountPercent;
        target.OrderCreditLimitEnabled = source.Order.IsMaximumAmountEnabled;
        target.OrderMaximumAmount = source.Order.MaximumAmount;
        target.OrderBelowMaximumAmountPercent = source.Order.CreditLimitBelowMaximumAmountPercent;
        target.OrderAboveMaximumAmountPercent = source.Order.CreditLimitAboveMaximumAmountPercent;
    }

    private static void UpsertBasicFields(PartyEntity entity, PartyDetailsDto party, DateTime refreshedAtUtc)
    {
        entity.BasicFields ??= new PartyBasicFieldsEntity { PartyId = party.Id };
        entity.BasicFields.Field1 = GetBasicValue(party.BasicFields, "PoleWlasne1");
        entity.BasicFields.Field2 = GetBasicValue(party.BasicFields, "PoleWlasne2");
        entity.BasicFields.Field3 = GetBasicValue(party.BasicFields, "PoleWlasne3");
        entity.BasicFields.Field4 = GetBasicValue(party.BasicFields, "PoleWlasne4");
        entity.BasicFields.Field5 = GetBasicValue(party.BasicFields, "PoleWlasne5");
        entity.BasicFields.Field6 = GetBasicValue(party.BasicFields, "PoleWlasne6");
        entity.BasicFields.Field7 = GetBasicValue(party.BasicFields, "PoleWlasne7");
        entity.BasicFields.Field8 = GetBasicValue(party.BasicFields, "PoleWlasne8");
        entity.BasicFields.PresentFieldsMask = GetBasicFieldsMask(party.BasicFields);
        entity.BasicFields.RefreshedAtUtc = refreshedAtUtc;
    }

    private void SynchronizeAdvancedFields(PartyEntity entity, PartyDetailsDto party, IReadOnlyDictionary<string, AdvancedFieldDefinitionDto> definitions, DateTime refreshedAtUtc)
    {
        var existing = entity.AdvancedFields.ToDictionary(field => field.FieldId, StringComparer.Ordinal);
        var current = party.AdvancedFields.ToDictionary(field => field.Id, StringComparer.Ordinal);

        foreach (var source in current.Values)
        {
            if (!definitions.TryGetValue(source.Id, out var definition))
                throw new InvalidOperationException($"Missing definition for advanced field '{source.Id}'.");

            if (!existing.Remove(source.Id, out var target))
            {
                target = new PartyAdvancedFieldEntity { PartyId = party.Id, FieldId = source.Id };
                entity.AdvancedFields.Add(target);
            }

            MapAdvancedValue(target, definition, source.Value);
            target.RefreshedAtUtc = refreshedAtUtc;
        }

        _dbContext.PartyAdvancedFields.RemoveRange(existing.Values);
    }

    private void SynchronizeAddresses(PartyEntity entity, PartyDetailsDto party, DateTime refreshedAtUtc)
    {
        var existing = entity.Addresses.ToDictionary(address => address.AddressId);
        var currentIds = new HashSet<int>();

        foreach (var (source, position) in party.Addresses.Select((address, index) => (address, index)))
        {
            var addressId = source.AddressId ?? throw new InvalidOperationException($"Address at position {position} for party '{party.Id}' has no Nexo id.");
            if (!currentIds.Add(addressId))
                throw new InvalidOperationException($"Party '{party.Id}' contains duplicate address id '{addressId}'.");

            if (!existing.Remove(addressId, out var target))
            {
                target = new PartyAddressEntity { PartyId = party.Id, AddressId = addressId };
                entity.Addresses.Add(target);
            }

            target.Street = source.Street;
            target.HouseNumber = source.HouseNumber;
            target.ApartmentNumber = source.ApartmentNumber;
            target.PostalCode = source.PostalCode;
            target.City = source.City;
            target.Municipality = source.Municipality;
            target.Voivodeship = source.Voivodeship;
            target.CountryId = source.CountryId;
            target.AddressTypeName = source.AddressTypeName;
            target.AddressTypeId = source.AddressTypeId;
            target.Position = position;
            target.RefreshedAtUtc = refreshedAtUtc;
        }

        _dbContext.PartyAddresses.RemoveRange(existing.Values);
    }

    private void SynchronizeContacts(PartyEntity entity, PartyDetailsDto party, DateTime refreshedAtUtc)
    {
        var existing = entity.Contacts.ToDictionary(contact => contact.ContactId);
        var currentIds = new HashSet<int>();

        foreach (var (source, position) in party.Contacts.Select((contact, index) => (contact, index)))
        {
            if (!currentIds.Add(source.ContactId))
                throw new InvalidOperationException($"Party '{party.Id}' contains duplicate contact id '{source.ContactId}'.");

            if (!existing.Remove(source.ContactId, out var target))
            {
                target = new PartyContactEntity { PartyId = party.Id, ContactId = source.ContactId };
                entity.Contacts.Add(target);
            }

            target.IsPrimary = source.IsPrimary;
            target.ContactTypeId = source.ContactTypeId;
            target.ContactType = source.ContactType;
            target.ContactValue = source.ContactValue;
            target.ContactDescription = source.ContactDescription;
            target.Position = position;
            target.RefreshedAtUtc = refreshedAtUtc;
        }

        _dbContext.PartyContacts.RemoveRange(existing.Values);
    }

    private void SynchronizeIndustries(PartyEntity entity, IReadOnlyList<string> industries, DateTime refreshedAtUtc)
    {
        var existing = entity.Industries.ToDictionary(industry => industry.Name, StringComparer.Ordinal);
        var currentNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (name, position) in industries.Select((name, index) => (name, index)))
        {
            if (!currentNames.Add(name))
                throw new InvalidOperationException($"Party '{entity.Id}' contains duplicate industry '{name}'.");

            if (!existing.Remove(name, out var target))
            {
                target = new PartyIndustryEntity { PartyId = entity.Id, Name = name };
                entity.Industries.Add(target);
            }

            target.Position = position;
            target.RefreshedAtUtc = refreshedAtUtc;
        }

        _dbContext.PartyIndustries.RemoveRange(existing.Values);
    }

    private void SynchronizeFeatures(PartyEntity entity, IReadOnlyList<string> features, DateTime refreshedAtUtc)
    {
        var existing = entity.Features.ToDictionary(feature => feature.Name, StringComparer.Ordinal);
        var currentNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (name, position) in features.Select((name, index) => (name, index)))
        {
            if (!currentNames.Add(name))
                throw new InvalidOperationException($"Party '{entity.Id}' contains duplicate feature '{name}'.");

            if (!existing.Remove(name, out var target))
            {
                target = new PartyFeatureEntity { PartyId = entity.Id, Name = name };
                entity.Features.Add(target);
            }

            target.Position = position;
            target.RefreshedAtUtc = refreshedAtUtc;
        }

        _dbContext.PartyFeatures.RemoveRange(existing.Values);
    }

    private static void MapAdvancedValue(PartyAdvancedFieldEntity target, AdvancedFieldDefinitionDto definition, object? value)
    {
        target.DataType = definition.DataType;
        target.TextValue = null;
        target.IntegerValue = null;
        target.DecimalValue = null;
        target.BooleanValue = null;
        target.DateValue = null;
        target.GuidValue = null;
        target.DictionaryIntegerKey = null;
        target.DictionaryGuidKey = null;

        switch (definition.DataType)
        {
            case AdvancedFieldDataType.Text:
            case AdvancedFieldDataType.LongText:
                target.TextValue = GetStringValue(value, definition.Id);
                return;
            case AdvancedFieldDataType.Integer:
                target.IntegerValue = GetStructValue<int>(value, definition.Id);
                return;
            case AdvancedFieldDataType.Decimal:
                target.DecimalValue = GetStructValue<decimal>(value, definition.Id);
                return;
            case AdvancedFieldDataType.Boolean:
                target.BooleanValue = GetStructValue<bool>(value, definition.Id);
                return;
            case AdvancedFieldDataType.Date:
                target.DateValue = GetStructValue<DateTime>(value, definition.Id);
                return;
            case AdvancedFieldDataType.Guid:
                target.GuidValue = GetStructValue<Guid>(value, definition.Id);
                return;
            case AdvancedFieldDataType.Dictionary:
                MapDictionaryValue(target, definition, value);
                return;
            case AdvancedFieldDataType.Unknown:
            default:
                throw new NotSupportedException($"Unsupported advanced field type for '{definition.Id}': {definition.DataType}.");
        }
    }

    private static void MapDictionaryValue(PartyAdvancedFieldEntity target, AdvancedFieldDefinitionDto definition, object? value)
    {
        var dictionary = definition.Dictionary ?? throw new InvalidOperationException($"Advanced field '{definition.Id}' has no dictionary definition.");

        if (dictionary.KeyType.Equals("int", StringComparison.OrdinalIgnoreCase) || dictionary.KeyType.Equals("System.Int32", StringComparison.Ordinal))
        {
            target.DictionaryIntegerKey = GetStructValue<int>(value, definition.Id);
            return;
        }

        if (dictionary.KeyType.Equals("guid", StringComparison.OrdinalIgnoreCase) || dictionary.KeyType.Equals("System.Guid", StringComparison.Ordinal))
        {
            target.DictionaryGuidKey = GetStructValue<Guid>(value, definition.Id);
            return;
        }

        throw new NotSupportedException($"Unsupported dictionary key type for '{definition.Id}': {dictionary.KeyType}.");
    }

    private static string? GetBasicValue(IReadOnlyCollection<AdditionalFieldValueDto> fields, string id) => GetStringValue(fields.SingleOrDefault(field => field.Id == id)?.Value, id);

    private static byte GetBasicFieldsMask(IReadOnlyCollection<AdditionalFieldValueDto> fields)
    {
        byte mask = 0;

        foreach (var field in fields)
        {
            const string prefix = "PoleWlasne";
            if (!field.Id.StartsWith(prefix, StringComparison.Ordinal) || !int.TryParse(field.Id[prefix.Length..], out var fieldNumber) || fieldNumber is < 1 or > 8)
                throw new InvalidOperationException($"Unsupported basic field id: {field.Id}.");

            mask |= (byte)(1 << (fieldNumber - 1));
        }

        return mask;
    }

    private static string? GetStringValue(object? value, string fieldId)
    {
        if (value is null)
            return null;
        if (value is string text)
            return text;
        if (value is JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined })
            return null;
        if (value is JsonElement { ValueKind: JsonValueKind.String } element)
            return element.GetString();

        throw InvalidFieldValue(fieldId, "a string");
    }

    private static T? GetStructValue<T>(object? value, string fieldId) where T : struct
    {
        if (value is null)
            return null;
        if (value is T typedValue)
            return typedValue;
        if (value is JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined })
            return null;
        if (value is JsonElement element)
        {
            try
            {
                return element.Deserialize<T>();
            }
            catch (JsonException exception)
            {
                throw InvalidFieldValue(fieldId, typeof(T).Name, exception);
            }
        }

        throw InvalidFieldValue(fieldId, typeof(T).Name);
    }

    private static InvalidOperationException InvalidFieldValue(string fieldId, string expectedType, Exception? innerException = null) => new($"Additional field '{fieldId}' must contain {expectedType}.", innerException);
}
