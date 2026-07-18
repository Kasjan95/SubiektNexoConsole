using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Application.Parties.GetPartyDetails
{
    public sealed class GetPartyDetailsHandler
    {
        private readonly IPartyRepository _repository;
        public GetPartyDetailsHandler(IPartyRepository repository)
        {
            _repository = repository;
        }
        public PartyDetailsDto? Handle(GetPartyDetailsQuery query)
        {
            return _repository.GetDetails(query);
        }
    }
}
