using SubiektNexoConnector.Core.Application.Common;

namespace SubiektNexoConnector.Core.Application.Parties.Addresses.PatchAddress;

public sealed record PatchPartyAddressCommand(
    string PartySignature,
    int AddressId,
    Optional<string?> Street,
    Optional<string?> HouseNumber,
    Optional<string?> ApartmentNumber,
    Optional<string?> PostalCode,
    Optional<string?> City,
    Optional<int?> CountryId);
