using SubiektNexoConnector.Core.Application.Parties.Addresses.Shared;
using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Application.Parties.Addresses.CreateAddress
{
    public sealed class CreatePartyAddressHandler
    {
        private readonly IPartyRepository _repository;
        public CreatePartyAddressHandler(IPartyRepository repository)
        {
            _repository = repository;
        }

        public CreatePartyAddressResult? Handle(CreatePartyAddressCommand command)
        {
            return _repository.CreatePartyAddress(command);
        }
    }
}
