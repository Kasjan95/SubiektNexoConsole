using SubiektNexoConnector.Core.Application.Common;
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

        return _repository.Patch(new PatchPartyCommand(
            command.PartySignature,
            NormalizeRequiredText(command.Signature, "Signature"),
            NormalizeRequiredText(command.DisplayName, "Display name"),
            command.IsActive,
            NormalizeOptionalText(command.FirstName),
            NormalizeOptionalText(command.LastName),
            NormalizeOptionalText(command.CompanyName),
            NormalizeOptionalText(command.TaxId),
            NormalizeOptionalText(command.EuTaxId),
            NormalizeOptionalText(command.BusinessRegistryNumber),
            NormalizeOptionalText(command.NationalCourtRegisterNumber),
            NormalizeOptionalText(command.PartyGroup),
            NormalizeTextList(command.Industries, "Industries"),
            NormalizeTextList(command.Features, "Features"),
            NormalizeOptionalText(command.Notes)));
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
        command.PartyGroup.HasValue ||
        command.Industries.HasValue ||
        command.Features.HasValue ||
        command.Notes.HasValue;

    private static Optional<string> NormalizeRequiredText(Optional<string> field, string fieldName)
    {
        if (!field.HasValue)
            return field;

        if (field.Value is null)
            throw new InvalidOperationException($"{fieldName} cannot be null.");

        var normalizedValue = field.Value.Trim();
        if (normalizedValue.Length == 0)
            throw new InvalidOperationException($"{fieldName} cannot be empty.");

        return normalizedValue;
    }

    private static Optional<string?> NormalizeOptionalText(Optional<string?> field)
    {
        if (!field.HasValue)
            return field;

        if (string.IsNullOrWhiteSpace(field.Value))
            return new Optional<string?>(null);

        return field.Value.Trim();
    }

    private static Optional<IReadOnlyCollection<string>> NormalizeTextList(
        Optional<IReadOnlyCollection<string>> field,
        string fieldName)
    {
        if (!field.HasValue)
            return field;

        if (field.Value is null)
            throw new InvalidOperationException($"{fieldName} cannot be null.");

        var normalizedValues = field.Value
            .Select(value => value?.Trim())
            .ToList();

        if (normalizedValues.Any(string.IsNullOrEmpty))
            throw new InvalidOperationException($"{fieldName} cannot contain empty values.");

        return new Optional<IReadOnlyCollection<string>>(
            normalizedValues
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList());
    }
}
