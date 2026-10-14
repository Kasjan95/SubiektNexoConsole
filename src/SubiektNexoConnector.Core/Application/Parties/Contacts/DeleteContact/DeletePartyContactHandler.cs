using SubiektNexoConnector.Core.Application.Parties.Shared;

using SubiektNexoConnector.Core.Application.Parties;

namespace SubiektNexoConnector.Core.Application.Parties.Contacts.DeleteContact
{
    public sealed class DeletePartyContactHandler
    {
        private readonly IPartyRepository _repository;
        private readonly IPartyReadModelSynchronizer _readModelSynchronizer;

        public DeletePartyContactHandler(IPartyRepository repository, IPartyReadModelSynchronizer readModelSynchronizer)
        {
            _repository = repository;
            _readModelSynchronizer = readModelSynchronizer;
        }

        public async Task<PartyDetailsDto?> HandleAsync(DeletePartyContactCommand command, CancellationToken cancellationToken = default)
        {
            var party = _repository.DeletePartyContact(command);
            if (party is null)
                return null;

            await _readModelSynchronizer.TryUpsertAsync(party, cancellationToken);
            return party;
        }
    }
}
