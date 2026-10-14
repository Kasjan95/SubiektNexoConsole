using Microsoft.EntityFrameworkCore;
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Common;
using SubiektNexoConnector.Core.Application.Parties;
using SubiektNexoConnector.Core.Application.Parties.Addresses.Shared;
using SubiektNexoConnector.Core.Application.Parties.Contacts.Shared;
using SubiektNexoConnector.Core.Application.Parties.GetParties;
using SubiektNexoConnector.Core.Application.Parties.Shared;
using SubiektNexoConnector.Infrastructure.Materialization.Persistence;

namespace SubiektNexoConnector.Infrastructure.Materialization.Parties;

internal sealed class PartyReadModelReader : IPartyReadModelReader
{
    private readonly ReadModelDbContext _dbContext;

    public PartyReadModelReader(ReadModelDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<PartyBasicDto>> GetAllAsync(GetPartiesQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var parties = _dbContext.Parties.AsNoTracking();
        parties = query.CustomerStatus switch
        {
            PartyCustomerStatusFilter.Standard => parties.Where(party => party.CustomerStatus == 0),
            PartyCustomerStatusFilter.Potential => parties.Where(party => party.CustomerStatus == 2),
            PartyCustomerStatusFilter.All => parties,
            _ => throw new ArgumentOutOfRangeException(nameof(query.CustomerStatus), query.CustomerStatus, "Unsupported party customer status filter.")
        };

        if (query.Type.HasValue)
            parties = parties.Where(party => party.Type == query.Type.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            parties = parties.Where(party =>
                party.Signature.Contains(search)
                || party.DisplayName.Contains(search)
                || party.TaxId != null && party.TaxId.Contains(search)
                || party.CompanyName != null && party.CompanyName.Contains(search)
                || party.FirstName != null && party.FirstName.Contains(search)
                || party.LastName != null && party.LastName.Contains(search));
        }

        return await parties
            .OrderBy(party => party.Id)
            .Skip(checked((query.Page - 1) * query.PageSize))
            .Take(query.PageSize)
            .Select(party => new PartyBasicDto(
                party.Id,
                party.Signature,
                party.DisplayName,
                new PartyClassificationDto(party.Type, party.TypeName, party.Subtype, party.SubtypeName, party.CustomerStatus, party.CustomerStatusName),
                party.TaxId,
                party.IsActive,
                party.FirstName,
                party.LastName,
                party.CompanyName))
            .ToListAsync(cancellationToken);
    }

    public async Task<MaterializedPartyDetails?> GetDetailsAsync(string signature, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signature);

        var party = await _dbContext.Parties
            .AsNoTracking()
            .AsSplitQuery()
            .Include(party => party.BasicFields)
            .Include(party => party.AdvancedFields)
            .Include(party => party.Addresses)
            .Include(party => party.Contacts)
            .Include(party => party.Industries)
            .Include(party => party.Features)
            .SingleOrDefaultAsync(party => party.Signature == signature, cancellationToken);

        return party is null ? null : new MaterializedPartyDetails(MapDetails(party), party.RefreshedAtUtc);
    }

    private static PartyDetailsDto MapDetails(PartyEntity party)
    {
        var basicFields = party.BasicFields ?? throw new InvalidOperationException($"Party '{party.Signature}' has no materialized basic fields.");

        return new PartyDetailsDto(
            party.Id,
            party.Signature,
            party.DisplayName,
            party.IsActive,
            new PartyClassificationDto(party.Type, party.TypeName, party.Subtype, party.SubtypeName, party.CustomerStatus, party.CustomerStatusName),
            party.FirstName,
            party.LastName,
            party.CompanyName,
            party.TaxId,
            party.EuTaxId,
            party.BusinessRegistryNumber,
            party.NationalCourtRegisterNumber,
            party.PartyGroup,
            party.Industries.OrderBy(industry => industry.Position).ThenBy(industry => industry.Name, StringComparer.Ordinal).Select(industry => industry.Name).ToArray(),
            party.Features.OrderBy(feature => feature.Position).ThenBy(feature => feature.Name, StringComparer.Ordinal).Select(feature => feature.Name).ToArray(),
            party.Notes,
            party.FlagId is int flagId ? new FlagAssignmentDto(flagId, party.FlagComment) : null,
            MapBasicFields(basicFields),
            MapAdvancedFields(party.AdvancedFields),
            party.Addresses.OrderBy(address => address.Position).ThenBy(address => address.AddressId).Select(MapAddress).ToArray(),
            party.Contacts.OrderBy(contact => contact.Position).ThenBy(contact => contact.ContactId).Select(MapContact).ToArray(),
            MapTradeCreditLimit(party));
    }

    private static IReadOnlyCollection<AdditionalFieldValueDto> MapBasicFields(PartyBasicFieldsEntity fields)
    {
        var values = new[] { fields.Field1, fields.Field2, fields.Field3, fields.Field4, fields.Field5, fields.Field6, fields.Field7, fields.Field8 };
        return values
            .Select((value, index) => new { Value = value, Index = index })
            .Where(field => (fields.PresentFieldsMask & (1 << field.Index)) != 0)
            .Select(field => new AdditionalFieldValueDto($"PoleWlasne{field.Index + 1}", field.Value))
            .ToArray();
    }

    private static IReadOnlyCollection<AdditionalFieldValueDto> MapAdvancedFields(IEnumerable<PartyAdvancedFieldEntity> fields) => fields.OrderBy(field => field.FieldId, StringComparer.Ordinal).Select(field => new AdditionalFieldValueDto(field.FieldId, GetAdvancedFieldValue(field))).ToArray();

    private static object? GetAdvancedFieldValue(PartyAdvancedFieldEntity field) => field.DataType switch
    {
        AdvancedFieldDataType.Text or AdvancedFieldDataType.LongText => field.TextValue,
        AdvancedFieldDataType.Integer => field.IntegerValue,
        AdvancedFieldDataType.Decimal => field.DecimalValue,
        AdvancedFieldDataType.Boolean => field.BooleanValue,
        AdvancedFieldDataType.Date => field.DateValue,
        AdvancedFieldDataType.Guid => field.GuidValue,
        AdvancedFieldDataType.Dictionary => GetDictionaryValue(field),
        _ => throw new NotSupportedException($"Unsupported advanced field type for '{field.FieldId}': {field.DataType}.")
    };

    private static object? GetDictionaryValue(PartyAdvancedFieldEntity field)
    {
        if (field.DictionaryIntegerKey.HasValue && field.DictionaryGuidKey.HasValue)
            throw new InvalidOperationException($"Advanced dictionary field '{field.FieldId}' contains both integer and GUID keys.");

        return field.DictionaryIntegerKey.HasValue ? field.DictionaryIntegerKey.Value : field.DictionaryGuidKey;
    }

    private static PartyAddressDto MapAddress(PartyAddressEntity address) => new(address.AddressId, address.Street, address.HouseNumber, address.ApartmentNumber, address.PostalCode, address.City, address.Municipality, address.Voivodeship, address.CountryId, address.AddressTypeName, address.AddressTypeId);

    private static PartyContactDto MapContact(PartyContactEntity contact) => new(contact.ContactId, contact.IsPrimary, contact.ContactTypeId, contact.ContactType, contact.ContactValue, contact.ContactDescription);

    private static TradeCreditLimitDto MapTradeCreditLimit(PartyEntity party) => new(
        party.TradeCreditEnabled,
        party.PaymentTermDays,
        party.MaximumPaymentDelayDays,
        party.MaximumUnpaidDocumentCount,
        party.OverallCreditLimit,
        new DocumentTradeCreditLimitDto(party.SalesCreditLimitEnabled, party.SalesMaximumAmount, party.SalesBelowMaximumAmountPercent, party.SalesAboveMaximumAmountPercent),
        new DocumentTradeCreditLimitDto(party.GoodsIssueCreditLimitEnabled, party.GoodsIssueMaximumAmount, party.GoodsIssueBelowMaximumAmountPercent, party.GoodsIssueAboveMaximumAmountPercent),
        new DocumentTradeCreditLimitDto(party.OrderCreditLimitEnabled, party.OrderMaximumAmount, party.OrderBelowMaximumAmountPercent, party.OrderAboveMaximumAmountPercent));
}
