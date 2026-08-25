using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;

namespace SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;

public sealed record AdvancedFieldDefinitionsDto(
    AdditionalFieldTarget Target,
    IReadOnlyCollection<AdvancedFieldGroupDto> Groups,
    IReadOnlyCollection<AdvancedFieldDefinitionDto> Fields
);

public sealed record AdvancedFieldGroupDto(
    string Name,
    int Position,
    IReadOnlyCollection<AdvancedFieldDefinitionDto> Fields
);

public sealed record AdvancedFieldDefinitionDto(
    string Id,
    string Name,
    string? Description,
    AdvancedFieldDataType DataType,
    bool Required,
    bool Visible,
    bool Editable,
    bool Cloneable,
    int? Precision,
    int? MinVisibleLines,
    int? MaxVisibleLines,
    object? DefaultValue,
    AdvancedFieldDictionaryDto? Dictionary
);

public enum AdvancedFieldDataType
{
    Text,
    LongText,
    Integer,
    Decimal,
    Boolean,
    Date,
    Guid,
    Dictionary,
    Unknown
}

public sealed record AdvancedFieldDictionaryDto(
    AdvancedFieldDictionaryKind Kind,
    string KeyType,
    string? SystemDictionary,
    IReadOnlyCollection<AdvancedFieldDictionaryOptionDto>? Options
);

public enum AdvancedFieldDictionaryKind
{
    Custom,
    CustomSql,
    System
}

public sealed record AdvancedFieldDictionaryOptionDto(
    string Key,
    string Label,
    bool? IsActive
);
