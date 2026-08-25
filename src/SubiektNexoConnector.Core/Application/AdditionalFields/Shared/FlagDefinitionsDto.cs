namespace SubiektNexoConnector.Core.Application.AdditionalFields.Shared
{
    public sealed record FlagDefinitionsDto(
        IReadOnlyCollection<FlagDomainDto> Domains
    );

    public sealed record FlagDomainDto(
        int? Id,
        string? Name,
        IReadOnlyCollection<FlagDefinitionDto> Flags
    );

    public sealed record FlagDefinitionDto(
        int Id,
        string Name,
        string? Description,
        string Color,
        string Shape,
        bool IsQuickFlag,
        bool IsAlwaysVisible
    );
}
