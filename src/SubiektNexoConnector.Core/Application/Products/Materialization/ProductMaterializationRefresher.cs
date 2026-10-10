using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.Materialization;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Core.Application.Products;

public sealed class ProductMaterializationRefresher : IProductMaterializationRefresher
{
    private readonly IProductRepository _products;
    private readonly IAdvancedFieldDefinitionsMaterializationRefresher _fieldDefinitions;
    private readonly IProductReadModelWriter _writer;
    private readonly IProductMaterializationCandidateStore _candidateStore;
    private readonly IMaterializationPolicyStore _policyStore;

    public ProductMaterializationRefresher(IProductRepository products, IAdvancedFieldDefinitionsMaterializationRefresher fieldDefinitions, IProductReadModelWriter writer, IProductMaterializationCandidateStore candidateStore, IMaterializationPolicyStore policyStore)
    {
        _products = products;
        _fieldDefinitions = fieldDefinitions;
        _writer = writer;
        _candidateStore = candidateStore;
        _policyStore = policyStore;
    }

    public async Task RefreshOneAsync(string sku, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        cancellationToken.ThrowIfCancellationRequested();

        var product = _products.GetDetails(sku, cancellationToken);
        if (product is null)
        {
            await _writer.DeleteAsync(sku, cancellationToken);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var definitions = await GetAdvancedFieldDefinitionsAsync([product], cancellationToken);
        await _writer.UpsertAsync(product, definitions, DateTime.UtcNow, cancellationToken);
    }

    public async Task<ProductMaterializationBatchResult> RefreshNextBatchAsync(int? afterId, int batchSize, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        cancellationToken.ThrowIfCancellationRequested();

        var candidates = _products.GetBatchAfterId(afterId, checked(batchSize + 1), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var products = candidates.Take(batchSize).ToArray();
        if (products.Length == 0)
            return new ProductMaterializationBatchResult(0, afterId, true);

        await RefreshSkusAsync(products.Select(product => product.SKU).ToArray(), cancellationToken);
        return new ProductMaterializationBatchResult(products.Length, products[^1].Id, candidates.Count <= batchSize);
    }

    public async Task<ProductMaterializationBatchResult> RefreshNextBatchAsync(MaterializationPolicyDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        cancellationToken.ThrowIfCancellationRequested();

        var policy = await _policyStore.GetOrCreateAsync(definition, cancellationToken);
        return policy.CandidateSelection switch
        {
            MaterializationCandidateSelection.ReadModel => await RefreshReadModelBatchAsync(policy, cancellationToken),
            MaterializationCandidateSelection.MissingFromReadModel => await RefreshMissingBatchAsync(policy, cancellationToken),
            _ => throw new NotSupportedException($"Unsupported materialization candidate selection '{policy.CandidateSelection}'.")
        };
    }

    private async Task<ProductMaterializationBatchResult> RefreshReadModelBatchAsync(MaterializationPolicy policy, CancellationToken cancellationToken)
    {
        var candidates = await _candidateStore.GetDueAsync(policy, DateTime.UtcNow, cancellationToken);
        await RefreshSkusAsync(candidates.Skus, cancellationToken);
        return new ProductMaterializationBatchResult(candidates.Skus.Count, null, !candidates.HasMore);
    }

    private async Task<ProductMaterializationBatchResult> RefreshMissingBatchAsync(MaterializationPolicy policy, CancellationToken cancellationToken)
    {
        var sourceCandidates = _products.GetAllMaterializationCandidates(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var existingIds = await _candidateStore.GetAllIdsAsync(cancellationToken);
        var missingSkus = sourceCandidates.Where(product => !existingIds.Contains(product.Id)).Select(product => product.Sku).ToArray();

        if (missingSkus.Length > 0)
        {
            var definitions = await GetAdvancedFieldDefinitionsAsync(cancellationToken);
            foreach (var batch in missingSkus.Chunk(policy.BatchSize))
                definitions = await RefreshSkusAsync(batch, definitions, cancellationToken);
        }

        return new ProductMaterializationBatchResult(missingSkus.Length, null, true);
    }

    private async Task RefreshSkusAsync(IReadOnlyCollection<string> skus, CancellationToken cancellationToken)
    {
        if (skus.Count == 0)
            return;

        cancellationToken.ThrowIfCancellationRequested();
        var products = _products.GetDetailsBatch(skus, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var definitions = await GetAdvancedFieldDefinitionsAsync(products, cancellationToken);
        await PersistProductsAsync(skus, products, definitions, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, AdvancedFieldDefinitionDto>> RefreshSkusAsync(IReadOnlyCollection<string> skus, IReadOnlyDictionary<string, AdvancedFieldDefinitionDto> definitions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var products = _products.GetDetailsBatch(skus, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var completeDefinitions = await EnsureDefinitionsCoverProductsAsync(products, definitions, cancellationToken);
        await PersistProductsAsync(skus, products, completeDefinitions, cancellationToken);
        return completeDefinitions;
    }

    private async Task PersistProductsAsync(IReadOnlyCollection<string> skus, IReadOnlyCollection<ProductDetailsDto> products, IReadOnlyDictionary<string, AdvancedFieldDefinitionDto> definitions, CancellationToken cancellationToken)
    {
        var refreshedAtUtc = DateTime.UtcNow;
        if (products.Count > 0)
            await _writer.UpsertBatchAsync(products, definitions, refreshedAtUtc, cancellationToken);

        var returnedSkus = products.Select(product => product.SKU).ToHashSet(StringComparer.Ordinal);
        foreach (var missingSku in skus.Where(sku => !returnedSkus.Contains(sku)))
            await _writer.DeleteAsync(missingSku, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, AdvancedFieldDefinitionDto>> GetAdvancedFieldDefinitionsAsync(CancellationToken cancellationToken)
    {
        var definitions = await _fieldDefinitions.GetCurrentAsync(AdditionalFieldTarget.Product, cancellationToken);
        return ToDefinitionDictionary(definitions);
    }

    private async Task<IReadOnlyDictionary<string, AdvancedFieldDefinitionDto>> GetAdvancedFieldDefinitionsAsync(IReadOnlyCollection<ProductDetailsDto> products, CancellationToken cancellationToken)
    {
        var definitions = await GetAdvancedFieldDefinitionsAsync(cancellationToken);
        return await EnsureDefinitionsCoverProductsAsync(products, definitions, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, AdvancedFieldDefinitionDto>> EnsureDefinitionsCoverProductsAsync(IReadOnlyCollection<ProductDetailsDto> products, IReadOnlyDictionary<string, AdvancedFieldDefinitionDto> definitions, CancellationToken cancellationToken)
    {
        if (products.SelectMany(product => product.AdvancedFields).All(field => definitions.ContainsKey(field.Id)))
            return definitions;

        var refreshed = await _fieldDefinitions.RefreshAsync(AdditionalFieldTarget.Product, cancellationToken);
        return ToDefinitionDictionary(refreshed.Definitions);
    }

    private static IReadOnlyDictionary<string, AdvancedFieldDefinitionDto> ToDefinitionDictionary(AdvancedFieldDefinitionsDto definitions) => definitions.Fields.Concat(definitions.Groups.SelectMany(group => group.Fields)).ToDictionary(definition => definition.Id, StringComparer.Ordinal);
}
