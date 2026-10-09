namespace SubiektNexoConnector.Core.Application.Products;

public sealed record ProductMaterializationCandidateBatch(
    IReadOnlyCollection<string> Skus,
    bool HasMore);
