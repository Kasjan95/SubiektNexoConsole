using System.Text.Json.Serialization;
using SubiektNexoConnector.Core.Application.Common;

namespace SubiektNexoConnector.Core.Application.Parties.PatchParty;

public sealed record PatchPartyRequestDto(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] Optional<string> Signature = default,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] Optional<string> DisplayName = default,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] Optional<bool> IsActive = default,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] Optional<string?> FirstName = default,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] Optional<string?> LastName = default,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] Optional<string?> CompanyName = default,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] Optional<string?> TaxId = default,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] Optional<string?> EuTaxId = default,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] Optional<string?> BusinessRegistryNumber = default,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] Optional<string?> NationalCourtRegisterNumber = default,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] Optional<string?> PartyGroup = default,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] Optional<IReadOnlyCollection<string>> Industries = default,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] Optional<IReadOnlyCollection<string>> Features = default,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] Optional<string?> Notes = default
);
