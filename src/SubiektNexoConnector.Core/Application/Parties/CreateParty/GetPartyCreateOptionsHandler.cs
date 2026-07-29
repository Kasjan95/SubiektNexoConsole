using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Application.Parties.CreateParty;

public sealed class GetPartyCreateOptionsHandler(IPartyRepository repository)
{
    public PartyCreateOptionsDto Handle() => repository.GetCreateOptions();
}
