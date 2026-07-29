namespace SubiektNexoConnector.Core.Application.Parties.CreateParty;

public sealed record PartyCreateOptionsDto(
    IReadOnlyCollection<PartyTypeOptionDto> PartyTypes,
    IReadOnlyCollection<ReferenceDataOptionDto> AddressTypes,
    IReadOnlyCollection<ReferenceDataOptionDto> ContactTypes,
    IReadOnlyCollection<CountryOptionDto> Countries,
    IReadOnlyCollection<ReferenceDataOptionDto> PartyGroups,
    IReadOnlyCollection<ReferenceDataOptionDto> Industries,
    IReadOnlyCollection<ReferenceDataOptionDto> Features);

public sealed record PartyTypeOptionDto(short Type, byte Subtype, string Name);

public sealed record ReferenceDataOptionDto(int Id, string Name);

public sealed record CountryOptionDto(int Id, string Name, string IsoAlpha2);
