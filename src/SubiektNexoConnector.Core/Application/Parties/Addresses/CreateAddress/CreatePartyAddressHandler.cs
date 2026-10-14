using SubiektNexoConnector.Core.Application.Parties.Addresses.Shared;
using SubiektNexoConnector.Core.Application.Parties.Shared;

using SubiektNexoConnector.Core.Application.Parties;

namespace SubiektNexoConnector.Core.Application.Parties.Addresses.CreateAddress
{
    public sealed class CreatePartyAddressHandler
    {
        private readonly IPartyRepository _repository;
        private readonly IPartyReadModelSynchronizer _readModelSynchronizer;

        public CreatePartyAddressHandler(IPartyRepository repository, IPartyReadModelSynchronizer readModelSynchronizer)
        {
            _repository = repository;
            _readModelSynchronizer = readModelSynchronizer;
        }

        public async Task<CreatePartyAddressResult?> HandleAsync(CreatePartyAddressCommand command, CancellationToken cancellationToken = default)
        {
            var result = _repository.CreatePartyAddress(command);
            if (result is null)
                return null;

            await _readModelSynchronizer.TryUpsertAsync(result.Party, cancellationToken);
            return result;
        }
    }
}
