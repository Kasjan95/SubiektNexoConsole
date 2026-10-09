using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;

namespace SubiektNexoConnector.Core.Application.Products;

public interface IProductReadModelWriter
{
    Task UpsertAsync(
        ProductDetailsDto product,
        IReadOnlyDictionary<string, AdvancedFieldDefinitionDto> advancedFieldDefinitions,
        DateTime refreshedAtUtc,
        CancellationToken cancellationToken = default);
    Task UpsertBatchAsync(
        IReadOnlyCollection<ProductDetailsDto> products,
        IReadOnlyDictionary<string, AdvancedFieldDefinitionDto> advancedFieldDefinitions,
        DateTime refreshedAtUtc,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(
        string sku,
        CancellationToken cancellationToken = default);
}
