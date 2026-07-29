using SubiektNexoConnector.Core.Application.Parties.Shared;
namespace SubiektNexoConnector.Core.Application.Parties.CreateParty
{
    public sealed class CreatePartyHandler
    {
        private readonly IPartyRepository _repository;
        
        public CreatePartyHandler(IPartyRepository repository)
        {
            _repository = repository;
        }
        public PartyDetailsDto Handle(CreatePartyCommand command)
        {
            return _repository.Create(command);
        }
    }
}
