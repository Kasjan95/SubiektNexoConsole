using SubiektNexoConnector.Core.Application.Parties.Shared;
using SubiektNexoConnector.Core.Application.Parties.Contacts.Shared;

using SubiektNexoConnector.Core.Application.Parties;

namespace SubiektNexoConnector.Core.Application.Parties.Contacts.CreateContact
{
    public sealed class CreatePartyContactHandler
    {
        private readonly IPartyRepository _repository;
        private readonly IPartyReadModelSynchronizer _readModelSynchronizer;

        public CreatePartyContactHandler(IPartyRepository repository, IPartyReadModelSynchronizer readModelSynchronizer)
        {
            _repository = repository;
            _readModelSynchronizer = readModelSynchronizer;
        }

        public async Task<PartyDetailsDto?> HandleAsync(CreatePartyContactCommand command, CancellationToken cancellationToken = default)
        {
            var party = _repository.CreatePartyContact(command);
            if (party is null)
                return null;

            await _readModelSynchronizer.TryUpsertAsync(party, cancellationToken);
            return party;
        }
    }
}
