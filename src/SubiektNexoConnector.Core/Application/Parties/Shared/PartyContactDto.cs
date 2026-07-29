namespace SubiektNexoConnector.Core.Application.Parties.Shared
{
    public sealed record PartyContactDto(
        int ContactId,
        bool IsPrimary,
        int ContactTypeId,
        string? ContactType,
        string? ContactValue,
        string? ContactDescription);
}
