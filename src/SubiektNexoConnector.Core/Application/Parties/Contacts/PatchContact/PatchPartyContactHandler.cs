using SubiektNexoConnector.Core.Application.Parties.Contacts.Shared;
using SubiektNexoConnector.Core.Application.Common;
using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Application.Parties.Contacts.PatchContact
{
    public sealed class PatchPartyContactHandler
    {
        private readonly IPartyRepository _repository;
        public PatchPartyContactHandler(IPartyRepository repository)
        {
            _repository = repository;
        }
        public PartyContactDto? Handle(PatchPartyContactCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);

            if (!HasChanges(command))
                throw new InvalidOperationException("At least one field must be provided.");

            return _repository.PatchPartyContact(new PatchPartyContactCommand(
                command.PartySignature,
                command.ContactId,
                command.IsPrimary,
                OptionalPatchNormalizer.OptionalText(command.ContactValue),
                OptionalPatchNormalizer.OptionalText(command.ContactDescription)));
        }

        private static bool HasChanges(PatchPartyContactCommand command) =>
            command.IsPrimary.HasValue ||
            command.ContactValue.HasValue ||
            command.ContactDescription.HasValue;
    }
}
