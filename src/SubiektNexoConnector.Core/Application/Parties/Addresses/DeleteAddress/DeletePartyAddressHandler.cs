using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Application.Parties.Addresses.DeleteAddress
{
    public sealed class DeletePartyAddressHandler
    {
        private readonly IPartyRepository _repository;

        public DeletePartyAddressHandler(IPartyRepository repository)
        {
            _repository = repository;
        }
        public DeletePartyResourceResult Handle(DeletePartyAddressCommand command)
        {
            return _repository.DeletePartyAddress(command);
        }
    }
}
