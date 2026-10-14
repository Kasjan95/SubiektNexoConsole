using SubiektNexoConnector.Core.Application.Parties.Contacts.Shared;
using SubiektNexoConnector.Core.Application.Common;
using SubiektNexoConnector.Core.Application.Parties.Shared;

using SubiektNexoConnector.Core.Application.Parties;

namespace SubiektNexoConnector.Core.Application.Parties.Contacts.PatchContact
{
    public sealed class PatchPartyContactHandler
    {
        private readonly IPartyRepository _repository;
        private readonly IPartyReadModelSynchronizer _readModelSynchronizer;

        public PatchPartyContactHandler(IPartyRepository repository, IPartyReadModelSynchronizer readModelSynchronizer)
        {
            _repository = repository;
            _readModelSynchronizer = readModelSynchronizer;
        }

        public async Task<PartyDetailsDto?> HandleAsync(PatchPartyContactCommand command, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);

            if (!HasChanges(command))
                throw new InvalidOperationException("At least one field must be provided.");

            var party = _repository.PatchPartyContact(new PatchPartyContactCommand(
                command.PartySignature,
                command.ContactId,
                command.IsPrimary,
                OptionalPatchNormalizer.OptionalText(command.ContactValue),
                OptionalPatchNormalizer.OptionalText(command.ContactDescription)));

            if (party is null)
                return null;

            await _readModelSynchronizer.TryUpsertAsync(party, cancellationToken);
            return party;
        }

        private static bool HasChanges(PatchPartyContactCommand command) =>
            command.IsPrimary.HasValue ||
            command.ContactValue.HasValue ||
            command.ContactDescription.HasValue;
    }
}
