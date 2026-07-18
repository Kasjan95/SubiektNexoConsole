
namespace SubiektNexoConnector.Core.Application.Parties.Shared
{
    public sealed record PartyAddressDto(
        int? AddressId,
        string? Street,
        string? HouseNumber,
        string? ApartmentNumber,
        string? City,
        string? Municipality,
        string? Voivodeship,
        string? Country,
        string? AddressTypeName,
        int? AddressTypeCode
        );
}
