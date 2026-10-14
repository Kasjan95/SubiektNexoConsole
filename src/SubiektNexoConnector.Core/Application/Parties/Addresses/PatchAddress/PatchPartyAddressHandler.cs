using SubiektNexoConnector.Core.Application.Common;
using SubiektNexoConnector.Core.Application.Parties.Addresses.Shared;
using SubiektNexoConnector.Core.Application.Parties.Shared;

using SubiektNexoConnector.Core.Application.Parties;

namespace SubiektNexoConnector.Core.Application.Parties.Addresses.PatchAddress;

public sealed class PatchPartyAddressHandler(IPartyRepository repository, IPartyReadModelSynchronizer readModelSynchronizer)
{
    public async Task<PartyDetailsDto?> HandleAsync(PatchPartyAddressCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!HasChanges(command))
            throw new InvalidOperationException("At least one field must be provided.");

        var party = repository.PatchPartyAddress(new PatchPartyAddressCommand(
            command.PartySignature,
            command.AddressId,
            OptionalPatchNormalizer.OptionalText(command.Street),
            OptionalPatchNormalizer.OptionalText(command.HouseNumber),
            OptionalPatchNormalizer.OptionalText(command.ApartmentNumber),
            OptionalPatchNormalizer.OptionalText(command.PostalCode),
            OptionalPatchNormalizer.OptionalText(command.City),
            command.CountryId));

        if (party is null)
            return null;

        await readModelSynchronizer.TryUpsertAsync(party, cancellationToken);
        return party;
    }

    private static bool HasChanges(PatchPartyAddressCommand command) =>
        command.Street.HasValue ||
        command.HouseNumber.HasValue ||
        command.ApartmentNumber.HasValue ||
        command.PostalCode.HasValue ||
        command.City.HasValue ||
        command.CountryId.HasValue;
}
