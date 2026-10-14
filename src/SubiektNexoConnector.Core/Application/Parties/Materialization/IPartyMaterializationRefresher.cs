namespace SubiektNexoConnector.Core.Application.Parties;

public interface IPartyMaterializationRefresher
{
    Task RefreshOneAsync(string signature, CancellationToken cancellationToken = default);
    Task<PartyMaterializationBatchResult> RefreshNextBatchAsync(int? afterId, int batchSize, CancellationToken cancellationToken = default);
    Task<int> CompleteCycleAsync(DateTime cycleStartedAtUtc, CancellationToken cancellationToken = default);
}
