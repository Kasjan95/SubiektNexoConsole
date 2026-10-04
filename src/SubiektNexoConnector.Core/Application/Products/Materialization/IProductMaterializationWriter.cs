using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;

namespace SubiektNexoConnector.Core.Application.Products;

public interface IProductMaterializationWriter
{
    Task UpsertAsync(
        ProductDetailsDto product,
        IReadOnlyDictionary<string, AdvancedFieldDefinitionDto> advancedFieldDefinitions,
        DateTime refreshedAtUtc,
        CancellationToken cancellationToken = default);
}
