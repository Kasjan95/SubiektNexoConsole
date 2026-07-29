using System.ComponentModel.DataAnnotations;

namespace SubiektNexoConnector.Core.Application.Parties.CreateParty;

public sealed record CreatePartyRequestDto(

    [Required(AllowEmptyStrings = false)]
    [MinLength(1)]
    string DisplayName,

    [Required]
    short? Type,

    [Required]
    byte? Subtype,

    string? Signature,
    string? FirstName,
    string? LastName,
    string? CompanyName,

    string? TaxId,
    string? EuTaxId,
    string? BusinessRegistryNumber,
    string? NationalCourtRegisterNumber,

    int? PartyGroupId,
    IReadOnlyCollection<int>? IndustryIds,
    IReadOnlyCollection<int>? FeatureIds,
    string? Notes,

    IReadOnlyCollection<PartyAddressRequestDto>? Addresses,
    IReadOnlyCollection<PartyContactRequestDto>? Contacts
);

public sealed record PartyAddressRequestDto(
    [Range(1, int.MaxValue)]
    int AddressTypeId,
    string? Street,
    string? HouseNumber,
    string? ApartmentNumber,
    string? PostalCode,
    string? City,
    int? CountryId
);

public sealed record PartyContactRequestDto(
    [Range(1, int.MaxValue)]
    int ContactTypeId,
    string? Value,
    bool IsPrimary,
    string? Comment
);
