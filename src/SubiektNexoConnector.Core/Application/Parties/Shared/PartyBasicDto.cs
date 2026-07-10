
namespace SubiektNexoConnector.Core.Application.Parties.Shared
{
    public sealed record PartyBasicDto(
        string Signature,
        string DisplayName,
        short Type,
        byte? Subtype,
        string TypeName,
        string SubtypeName,
        int CustomerStatus,
        string CustomerStatusName,
        string? TaxId,
        bool IsActive,
        string? FirstName,
        string? LastName,
        string? CompanyName
    );
}
