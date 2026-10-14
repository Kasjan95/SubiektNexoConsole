using SubiektNexoConnector.Core.Application.Parties.GetParties;
using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Application.Parties;

public interface IPartyReadModelReader
{
    Task<IReadOnlyCollection<PartyBasicDto>> GetAllAsync(GetPartiesQuery query, CancellationToken cancellationToken = default);
    Task<MaterializedPartyDetails?> GetDetailsAsync(string signature, CancellationToken cancellationToken = default);
}
