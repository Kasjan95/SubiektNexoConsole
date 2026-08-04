using SubiektNexoConnector.Core.Application.AdditionalFields.GetFieldsType;

namespace SubiektNexoConnector.Core.Application.AdditionalFields.Shared;

public sealed record AdditionalFieldsDefinitionDto(
    AdditionalFieldTarget Target,
    IReadOnlyCollection<AdditionalFieldGroupDto> Groups,
    IReadOnlyCollection<AdditionalFieldDefinitionDto> Fields
);

public sealed record AdditionalFieldGroupDto(
    string Name,
    int Position,
    IReadOnlyCollection<AdditionalFieldDefinitionDto> Fields
);

public sealed record AdditionalFieldDefinitionDto(
    string Id,
    string Name,
    string? Description,
    AdditionalFieldDataType DataType,
    bool Required,
    bool Visible,
    bool Editable,
    bool Cloneable,
    int? Precision,
    int? MinVisibleLines,
    int? MaxVisibleLines,
    object? DefaultValue,
    AdditionalFieldDictionaryDto? Dictionary
);

public enum AdditionalFieldDataType
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

public sealed record AdditionalFieldDictionaryDto(
    AdditionalFieldDictionaryKind Kind,
    string KeyType,
    string? SystemDictionary,
    IReadOnlyCollection<AdditionalFieldDictionaryOptionDto>? Options
);

public enum AdditionalFieldDictionaryKind
{
    Custom,
    CustomSql,
    System
}

public sealed record AdditionalFieldDictionaryOptionDto(
    string Key,
    string Label,
    bool? IsActive
);
