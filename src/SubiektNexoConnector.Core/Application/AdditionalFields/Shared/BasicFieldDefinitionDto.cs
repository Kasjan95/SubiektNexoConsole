namespace SubiektNexoConnector.Core.Application.AdditionalFields.Shared;

public enum AdditionalFieldTarget
{
    Product,
    Party
}

public sealed record BasicFieldDefinitionsDto(
    AdditionalFieldTarget Target,
    IReadOnlyCollection<BasicFieldDefinitionDto> Fields);

public sealed record BasicFieldDefinitionDto(
    string Id,
    string Name,
    bool IsActive);
