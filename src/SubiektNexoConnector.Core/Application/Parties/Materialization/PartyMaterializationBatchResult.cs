namespace SubiektNexoConnector.Core.Application.Parties;

public sealed record PartyMaterializationBatchResult(int ProcessedCount, int? NextCursor, bool CycleCompleted);
