using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class PartyAdvancedFieldEntity
{
    public int PartyId { get; set; }
    public required string FieldId { get; set; }
    public AdvancedFieldDataType DataType { get; set; }
    public string? TextValue { get; set; }
    public int? IntegerValue { get; set; }
    public decimal? DecimalValue { get; set; }
    public bool? BooleanValue { get; set; }
    public DateTime? DateValue { get; set; }
    public Guid? GuidValue { get; set; }
    public int? DictionaryIntegerKey { get; set; }
    public Guid? DictionaryGuidKey { get; set; }
    public DateTime RefreshedAtUtc { get; set; }
}
