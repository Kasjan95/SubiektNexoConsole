namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class PartyAddressEntity
{
    public int PartyId { get; set; }
    public int AddressId { get; set; }
    public string? Street { get; set; }
    public string? HouseNumber { get; set; }
    public string? ApartmentNumber { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Municipality { get; set; }
    public string? Voivodeship { get; set; }
    public int? CountryId { get; set; }
    public string? AddressTypeName { get; set; }
    public int? AddressTypeId { get; set; }
    public int Position { get; set; }
    public DateTime RefreshedAtUtc { get; set; }
}
