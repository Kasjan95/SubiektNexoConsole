using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Application.Parties.Contacts.DeleteContact
{
    public sealed class DeletePartyContactHandler
    {
        private readonly IPartyRepository _repository;
        public DeletePartyContactHandler(IPartyRepository repository)
        {
            _repository = repository;
        }
        public DeletePartyResourceResult Handle(DeletePartyContactCommand command)
        {
            return _repository.DeletePartyContact(command);
        }
    }
}
