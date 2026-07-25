using SubiektNexoConnector.Core.Application.Parties.GetParties;
using SubiektNexoConnector.Core.Application.Parties.GetPartyDetails;
using SubiektNexoConnector.Core.Application.Parties.PatchParty;

namespace SubiektNexoConnector.Core.Application.Parties.Shared
{
    public interface IPartyRepository
    {
        IReadOnlyCollection<PartyBasicDto> GetAll(GetPartiesQuery query);
        PartyDetailsDto? GetDetails(GetPartyDetailsQuery query);
        string? Patch(PatchPartyCommand command);
    }
}
