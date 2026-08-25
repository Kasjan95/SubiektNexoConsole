namespace SubiektNexoConnector.Core.Application.Parties.CreateParty;

public sealed record CreatePartyCommand(
    string DisplayName,
    short Type,
    byte Subtype,

    string? Signature,
    string? FirstName,
    string? LastName,
    string? CompanyName,

    string? TaxId,
    string? EuTaxId,
    string? BusinessRegistryNumber,
    string? NationalCourtRegisterNumber,

    int? PartyGroupId,
    IReadOnlyCollection<int> IndustryIds,
    IReadOnlyCollection<int> FeatureIds,
    string? Notes,

    IReadOnlyCollection<PartyAddressInput> Addresses,
    IReadOnlyCollection<PartyContactInput> Contacts
);

public sealed record PartyAddressInput(
    int AddressTypeId,
    string? Street,
    string? HouseNumber,
    string? ApartmentNumber,
    string? PostalCode,
    string? City,
    int? CountryId
);

public sealed record PartyContactInput(
    int ContactTypeId,
    string? Value,
    bool IsPrimary,
    string? Comment
);
