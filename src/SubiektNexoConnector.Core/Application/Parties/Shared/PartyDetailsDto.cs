namespace SubiektNexoConnector.Core.Application.Parties.Shared
{
    public sealed record PartyDetailsDto(
        string Signature,
        string DisplayName,
        bool IsActive,

        string TypeName,
        string SubtypeName,

        string? FirstName,
        string? LastName,
        string? CompanyName,

        string? TaxId,
        string? EuTaxId,
        string? BusinessRegistryNumber,
        string? NationalCourtRegisterNumber,

        string? PartyGroup,
        string? Industry,
        IReadOnlyList<string> Features,

        string? Notes,

        IReadOnlyCollection<PartyAddressDto> Addresses,
        IReadOnlyCollection<PartyContactDto> Contacts,

        TradeCreditLimitDto TradeCreditLimit
    );
}
