namespace SubiektNexoConnector.Core.Application.Parties.Shared
{
    public sealed record PartyContactDto(
        int ContactId,
        bool IsPrimary,
        string? ContactType,
        string? ContactValue,
        string? ContactDescription);
}
