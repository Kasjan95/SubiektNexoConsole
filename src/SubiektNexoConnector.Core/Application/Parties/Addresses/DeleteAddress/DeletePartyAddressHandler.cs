using SubiektNexoConnector.Core.Application.Parties.Shared;

using SubiektNexoConnector.Core.Application.Parties;

namespace SubiektNexoConnector.Core.Application.Parties.Addresses.DeleteAddress
{
    public sealed class DeletePartyAddressHandler
    {
        private readonly IPartyRepository _repository;
        private readonly IPartyReadModelSynchronizer _readModelSynchronizer;

        public DeletePartyAddressHandler(IPartyRepository repository, IPartyReadModelSynchronizer readModelSynchronizer)
        {
            _repository = repository;
            _readModelSynchronizer = readModelSynchronizer;
        }

        public async Task<PartyDetailsDto?> HandleAsync(DeletePartyAddressCommand command, CancellationToken cancellationToken = default)
        {
            var party = _repository.DeletePartyAddress(command);
            if (party is null)
                return null;

            await _readModelSynchronizer.TryUpsertAsync(party, cancellationToken);
            return party;
        }
    }
}
