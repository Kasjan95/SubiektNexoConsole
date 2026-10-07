namespace SubiektNexoConnector.Core.Application.Products;

public sealed record ProductMaterializationBatchResult(
    int ProcessedCount,
    int? NextCursor,
    bool CycleCompleted);
