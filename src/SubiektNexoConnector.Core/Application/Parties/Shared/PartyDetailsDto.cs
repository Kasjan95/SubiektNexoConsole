namespace SubiektNexoConnector.Core.Application.Parties.Shared
{
    using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
    using SubiektNexoConnector.Core.Application.Common;
    using SubiektNexoConnector.Core.Application.Parties.Addresses.Shared;
    using SubiektNexoConnector.Core.Application.Parties.Contacts.Shared;

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
        IReadOnlyList<string> Industries,
        IReadOnlyList<string> Features,

        string? Notes,
        FlagAssignmentDto? Flag,
        IReadOnlyCollection<AdditionalFieldValueDto> BasicFields,
        IReadOnlyCollection<AdditionalFieldValueDto> AdvancedFields,

        IReadOnlyCollection<PartyAddressDto> Addresses,
        IReadOnlyCollection<PartyContactDto> Contacts,

        TradeCreditLimitDto TradeCreditLimit
    );
}
