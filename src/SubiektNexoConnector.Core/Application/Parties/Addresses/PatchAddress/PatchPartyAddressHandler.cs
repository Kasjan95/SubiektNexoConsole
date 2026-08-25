using SubiektNexoConnector.Core.Application.Common;
using SubiektNexoConnector.Core.Application.Parties.Addresses.Shared;
using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Application.Parties.Addresses.PatchAddress;

public sealed class PatchPartyAddressHandler(IPartyRepository repository)
{
    public PartyAddressDto? Handle(PatchPartyAddressCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!HasChanges(command))
            throw new InvalidOperationException("At least one field must be provided.");

        return repository.PatchPartyAddress(new PatchPartyAddressCommand(
            command.PartySignature,
            command.AddressId,
            OptionalPatchNormalizer.OptionalText(command.Street),
            OptionalPatchNormalizer.OptionalText(command.HouseNumber),
            OptionalPatchNormalizer.OptionalText(command.ApartmentNumber),
            OptionalPatchNormalizer.OptionalText(command.PostalCode),
            OptionalPatchNormalizer.OptionalText(command.City),
            command.CountryId));
    }

    private static bool HasChanges(PatchPartyAddressCommand command) =>
        command.Street.HasValue ||
        command.HouseNumber.HasValue ||
        command.ApartmentNumber.HasValue ||
        command.PostalCode.HasValue ||
        command.City.HasValue ||
        command.CountryId.HasValue;
}
