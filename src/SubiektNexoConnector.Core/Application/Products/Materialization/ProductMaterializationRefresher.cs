using SubiektNexoConnector.Core.Application.AdditionalFields.GetAdvancedFieldDefinitions;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;

namespace SubiektNexoConnector.Core.Application.Products;

public sealed class ProductMaterializationRefresher : IProductMaterializationRefresher
{
    private readonly IProductRepository _products;
    private readonly IAdditionalFieldDefinitionRepository _fieldDefinitions;
    private readonly IProductReadModelWriter _writer;

    public ProductMaterializationRefresher(
        IProductRepository products,
        IAdditionalFieldDefinitionRepository fieldDefinitions,
        IProductReadModelWriter writer)
    {
        _products = products;
        _fieldDefinitions = fieldDefinitions;
        _writer = writer;
    }

    public async Task RefreshOneAsync(string sku, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        cancellationToken.ThrowIfCancellationRequested();

        var product = _products.GetDetails(sku);

        if (product is null)
        {
            await _writer.DeleteAsync(sku, cancellationToken);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var definitions = GetAdvancedFieldDefinitions();

        await _writer.UpsertAsync(
            product,
            definitions,
            DateTime.UtcNow,
            cancellationToken);
    }

    public async Task<ProductMaterializationBatchResult> RefreshNextBatchAsync(
        int? afterId,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        cancellationToken.ThrowIfCancellationRequested();

        var candidates = _products.GetBatchAfterId(afterId, checked(batchSize + 1));
        cancellationToken.ThrowIfCancellationRequested();

        var products = candidates.Take(batchSize).ToArray();
        if (products.Length == 0)
            return new ProductMaterializationBatchResult(0, afterId, true);

        var definitions = GetAdvancedFieldDefinitions();

        foreach (var product in products)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var details = _products.GetDetails(product.SKU);

            if (details is null)
            {
                await _writer.DeleteAsync(product.SKU, cancellationToken);
                continue;
            }

            await _writer.UpsertAsync(
                details,
                definitions,
                DateTime.UtcNow,
                cancellationToken);
        }

        return new ProductMaterializationBatchResult(
            products.Length,
            products[^1].Id,
            candidates.Count <= batchSize);
    }

    private IReadOnlyDictionary<string, AdvancedFieldDefinitionDto> GetAdvancedFieldDefinitions() =>
        _fieldDefinitions
            .GetAdvancedFieldDefinitions(
                new GetAdvancedFieldDefinitionsQuery(AdditionalFieldTarget.Product))
            .Fields
            .ToDictionary(
                definition => definition.Id,
                StringComparer.Ordinal);
}
