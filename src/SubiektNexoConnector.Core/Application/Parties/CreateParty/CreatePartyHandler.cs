using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Application.Parties.CreateParty;

public sealed class CreatePartyHandler
{
    private readonly IPartyRepository _repository;
    private readonly IPartyReadModelSynchronizer _readModelSynchronizer;

    public CreatePartyHandler(IPartyRepository repository, IPartyReadModelSynchronizer readModelSynchronizer)
    {
        _repository = repository;
        _readModelSynchronizer = readModelSynchronizer;
    }

    public async Task<PartyDetailsDto> HandleAsync(CreatePartyCommand command, CancellationToken cancellationToken = default)
    {
        var party = _repository.CreateParty(command);
        await _readModelSynchronizer.TryUpsertAsync(party, cancellationToken);
        return party;
    }
}
