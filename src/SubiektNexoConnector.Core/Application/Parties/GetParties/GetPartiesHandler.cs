using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Application.Parties.GetParties
{
    public sealed class GetPartiesHandler
    {
        private readonly IPartyRepository _repository;

        public GetPartiesHandler(IPartyRepository repository)
        {
            _repository = repository;
        }

        public IReadOnlyCollection<PartyBasicDto> Handle(GetPartiesQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);
            return _repository.GetAllParties(query);
        }
    }
}
