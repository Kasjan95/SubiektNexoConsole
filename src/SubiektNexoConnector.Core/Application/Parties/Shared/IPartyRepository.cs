using SubiektNexoConnector.Core.Application.Parties.GetParties;
using SubiektNexoConnector.Core.Application.Parties.GetPartyDetails;

namespace SubiektNexoConnector.Core.Application.Parties.Shared
{
    public interface IPartyRepository
    {
        IReadOnlyCollection<PartyBasicDto> GetAll(GetPartiesQuery query);
        PartyDetailsDto? GetDetails(GetPartyDetailsQuery query);
    }
}
