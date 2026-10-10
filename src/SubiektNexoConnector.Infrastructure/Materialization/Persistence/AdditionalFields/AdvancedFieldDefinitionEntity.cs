using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class AdvancedFieldDefinitionEntity
{
    public long Id { get; set; }
    public AdditionalFieldTarget Target { get; set; }
    public required string FieldId { get; set; }

    public required string Name { get; set; }
    public string? Description { get; set; }
    public AdvancedFieldDataType DataType { get; set; }

    public bool Required { get; set; }
    public bool Visible { get; set; }
    public bool Editable { get; set; }
    public bool Cloneable { get; set; }

    public int? Precision { get; set; }
    public int? MinVisibleLines { get; set; }
    public int? MaxVisibleLines { get; set; }
    public string? DefaultValueJson { get; set; }

    public int Position { get; set; }

    public AdvancedFieldDictionaryKind? DictionaryKind { get; set; }
    public string? DictionaryKeyType { get; set; }
    public string? SystemDictionary { get; set; }
    public bool DictionaryOptionsAvailable { get; set; }

    public long? GroupId { get; set; }
    public AdvancedFieldGroupEntity? Group { get; set; }
    public ICollection<AdvancedFieldDictionaryOptionEntity> DictionaryOptions { get; set; } = [];
}
