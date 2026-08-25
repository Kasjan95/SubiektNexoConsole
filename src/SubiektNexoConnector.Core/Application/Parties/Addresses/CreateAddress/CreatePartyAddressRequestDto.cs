using System.ComponentModel.DataAnnotations;

namespace SubiektNexoConnector.Core.Application.Parties.Addresses.CreateAddress;

public sealed record CreatePartyAddressRequestDto(
    [Range(1, int.MaxValue)] int AddressTypeId,
    string? Street,
    string? HouseNumber,
    string? ApartmentNumber,
    string? PostalCode,
    string? City,
    [Range(1, int.MaxValue)] int? CountryId);
