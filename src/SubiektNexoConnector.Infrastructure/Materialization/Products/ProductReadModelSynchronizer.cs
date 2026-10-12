using Microsoft.Extensions.Logging;
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.Materialization;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Products;

namespace SubiektNexoConnector.Infrastructure.Materialization.Products;

internal sealed class ProductReadModelSynchronizer : IProductReadModelSynchronizer
{
    private readonly IAdvancedFieldDefinitionsReadModelReader _fieldDefinitionsReader;
    private readonly IProductReadModelWriter _writer;
    private readonly ILogger<ProductReadModelSynchronizer> _logger;

    public ProductReadModelSynchronizer(IAdvancedFieldDefinitionsReadModelReader fieldDefinitionsReader, IProductReadModelWriter writer, ILogger<ProductReadModelSynchronizer> logger)
    {
        _fieldDefinitionsReader = fieldDefinitionsReader;
        _writer = writer;
        _logger = logger;
    }

    public async Task<bool> TryUpsertAsync(ProductDetailsDto product, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(product);

        try
        {
            var definitions = product.AdvancedFields.Count == 0
                ? new Dictionary<string, AdvancedFieldDefinitionDto>(StringComparer.Ordinal)
                : ToDefinitionDictionary(await _fieldDefinitionsReader.GetAsync(AdditionalFieldTarget.Product, cancellationToken));

            await _writer.UpsertAsync(product, definitions, DateTime.UtcNow, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Product {ProductSku} was saved in Nexo, but its SQLite materialization could not be updated.", product.SKU);
            return false;
        }
    }

    public async Task<bool> TryDeleteAsync(string sku, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        try
        {
            await _writer.DeleteAsync(sku, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Product {ProductSku} is absent from Nexo, but its SQLite materialization could not be deleted.", sku);
            return false;
        }
    }

    private static IReadOnlyDictionary<string, AdvancedFieldDefinitionDto> ToDefinitionDictionary(AdvancedFieldDefinitionsDto definitions)
    {
        var definitionsWithoutGroup = definitions.Fields;
        var definitionsFromGroups = definitions.Groups.SelectMany(group => group.Fields);
        var allDefinitions = definitionsWithoutGroup.Concat(definitionsFromGroups);

        return allDefinitions.ToDictionary(definition => definition.Id, StringComparer.Ordinal);
    }
}
