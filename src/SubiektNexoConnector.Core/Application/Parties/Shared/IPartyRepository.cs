using SubiektNexoConnector.Core.Application.Parties.GetParties;

namespace SubiektNexoConnector.Core.Application.Parties.Shared
{
    public interface IPartyRepository
    {
        IReadOnlyCollection<PartyBasicDto> GetAll(GetPartiesQuery query);
    }
}
