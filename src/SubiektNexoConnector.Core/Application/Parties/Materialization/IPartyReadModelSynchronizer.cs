using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Application.Parties;

public interface IPartyReadModelSynchronizer
{
    Task<bool> TryUpsertAsync(PartyDetailsDto party, CancellationToken cancellationToken = default);
}
