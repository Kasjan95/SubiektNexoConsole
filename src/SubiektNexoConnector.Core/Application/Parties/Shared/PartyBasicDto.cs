using SubiektNexoConnector.Core.Application.Common;

namespace SubiektNexoConnector.Core.Application.Parties.Shared
{
    public sealed record PartyBasicDto(
        int Id,
        string Signature,
        string DisplayName,
        PartyClassificationDto Classification,
        string? TaxId,
        bool IsActive,
        string? FirstName,
        string? LastName,
        string? CompanyName
    ) : ISearchable
    {
        public string GetSearchText()
        {
            return string.Join(
                ' ',
                new[]
                {
                    Signature,
                    DisplayName,
                    TaxId,
                    CompanyName,
                    FirstName,
                    LastName
                }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }
    }
}
