using SubiektNexoConnector.Core.Application.Parties.Shared;
using SubiektNexoConnector.Core.Application.Parties.Contacts.Shared;

namespace SubiektNexoConnector.Core.Application.Parties.Contacts.CreateContact
{
    public sealed class CreatePartyContactHandler
    {
        private readonly IPartyRepository _repository;
        public CreatePartyContactHandler(IPartyRepository repository)
        {
            _repository = repository;
        }
        public PartyContactDto? Handle(CreatePartyContactCommand command)
        {
            return _repository.CreatePartyContact(command);
        }
    }
}
