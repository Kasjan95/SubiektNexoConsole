using SubiektNexoConnector.Core.Application.Common;

namespace SubiektNexoConnector.Core.Application.Parties.PatchParty;

public sealed record PatchPartyCommand(
    string PartySignature,
    Optional<string> Signature,
    Optional<string> DisplayName,
    Optional<bool> IsActive,
    Optional<string?> FirstName,
    Optional<string?> LastName,
    Optional<string?> CompanyName,
    Optional<string?> TaxId,
    Optional<string?> EuTaxId,
    Optional<string?> BusinessRegistryNumber,
    Optional<string?> NationalCourtRegisterNumber,
    Optional<int?> PartyGroupId,
    Optional<IReadOnlyCollection<int>> IndustryIds,
    Optional<IReadOnlyCollection<int>> FeatureIds,
    Optional<string?> Notes
);
