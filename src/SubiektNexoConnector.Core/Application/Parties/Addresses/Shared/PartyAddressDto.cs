namespace SubiektNexoConnector.Core.Application.Parties.Addresses.Shared
{
    public sealed record PartyAddressDto(
        int? AddressId,
        string? Street,
        string? HouseNumber,
        string? ApartmentNumber,
        string? PostalCode,
        string? City,
        string? Municipality,
        string? Voivodeship,
        int? CountryId,
        string? AddressTypeName,
        int? AddressTypeId
        );
}
