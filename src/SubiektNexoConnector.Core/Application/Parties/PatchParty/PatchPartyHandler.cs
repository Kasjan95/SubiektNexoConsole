using SubiektNexoConnector.Core.Application.Common;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Application.Parties.PatchParty;

public sealed class PatchPartyHandler
{
    private readonly IPartyRepository _repository;

    public PatchPartyHandler(IPartyRepository repository)
    {
        _repository = repository;
    }

    public string? Handle(PatchPartyCommand command)
    {
        if (!HasChanges(command))
            throw new InvalidOperationException("At least one field must be provided.");

        return _repository.PatchParty(new PatchPartyCommand(
            command.PartySignature,
            OptionalPatchNormalizer.RequiredText(command.Signature, "Signature"),
            OptionalPatchNormalizer.RequiredText(command.DisplayName, "Display name"),
            command.IsActive,
            OptionalPatchNormalizer.OptionalText(command.FirstName),
            OptionalPatchNormalizer.OptionalText(command.LastName),
            OptionalPatchNormalizer.OptionalText(command.CompanyName),
            OptionalPatchNormalizer.OptionalText(command.TaxId),
            OptionalPatchNormalizer.OptionalText(command.EuTaxId),
            OptionalPatchNormalizer.OptionalText(command.BusinessRegistryNumber),
            OptionalPatchNormalizer.OptionalText(command.NationalCourtRegisterNumber),
            command.PartyGroupId,
            OptionalPatchNormalizer.PositiveDistinctIds(command.IndustryIds, "Industry IDs"),
            OptionalPatchNormalizer.PositiveDistinctIds(command.FeatureIds, "Feature IDs"),
            OptionalPatchNormalizer.OptionalText(command.Notes),
            OptionalPatchNormalizer.AdditionalFields(command.BasicFields, "BasicFields"),
            OptionalPatchNormalizer.AdditionalFields(command.AdvancedFields, "AdvancedFields"),
            OptionalPatchNormalizer.Flag(command.Flag)));
    }

    private static bool HasChanges(PatchPartyCommand command) =>
        command.Signature.HasValue ||
        command.DisplayName.HasValue ||
        command.IsActive.HasValue ||
        command.FirstName.HasValue ||
        command.LastName.HasValue ||
        command.CompanyName.HasValue ||
        command.TaxId.HasValue ||
        command.EuTaxId.HasValue ||
        command.BusinessRegistryNumber.HasValue ||
        command.NationalCourtRegisterNumber.HasValue ||
        command.PartyGroupId.HasValue ||
        command.IndustryIds.HasValue ||
        command.FeatureIds.HasValue ||
        command.Notes.HasValue ||
        command.BasicFields.HasValue ||
        command.AdvancedFields.HasValue ||
        command.Flag.HasValue;

}
