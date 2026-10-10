using System.Text.Json;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Products;
using SubiektNexoConnector.Infrastructure.Materialization.Persistence;

namespace SubiektNexoConnector.Infrastructure.Materialization.Products;

internal sealed class ProductReadModelWriter : IProductReadModelWriter
{
    private readonly ReadModelDbContext _dbContext;
    private readonly ILogger<ProductReadModelWriter> _logger;

    public ProductReadModelWriter(ReadModelDbContext dbContext, ILogger<ProductReadModelWriter>? logger = null)
    {
        _dbContext = dbContext;
        _logger = logger ?? NullLogger<ProductReadModelWriter>.Instance;
    }

    public async Task UpsertAsync(ProductDetailsDto product, IReadOnlyDictionary<string, AdvancedFieldDefinitionDto> advancedFieldDefinitions, DateTime refreshedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(advancedFieldDefinitions);

        await UpsertCoreAsync(
            product,
            advancedFieldDefinitions,
            refreshedAtUtc,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpsertBatchAsync(IReadOnlyCollection<ProductDetailsDto> products, IReadOnlyDictionary<string, AdvancedFieldDefinitionDto> advancedFieldDefinitions, DateTime refreshedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(advancedFieldDefinitions);

        if (products.Count == 0)
            return;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            foreach (var product in products)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UpsertCoreAsync(
                    product,
                    advancedFieldDefinitions,
                    refreshedAtUtc,
                    cancellationToken);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _dbContext.ChangeTracker.Clear();
        }

        stopwatch.Stop();
        var productsPerSecond = products.Count / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001);
        _logger.LogInformation(
            "Persisted product materialization batch in SQLite. Products: {ProductCount}; duration: {DurationMs} ms; rate: {ProductsPerSecond} products/s.",
            products.Count,
            stopwatch.Elapsed.TotalMilliseconds,
            productsPerSecond);
    }

    private async Task UpsertCoreAsync(ProductDetailsDto product, IReadOnlyDictionary<string, AdvancedFieldDefinitionDto> advancedFieldDefinitions, DateTime refreshedAtUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(product);

        var entity = await _dbContext.Products.FindAsync([product.Id], cancellationToken);

        if (entity is null)
        {
            entity = new ProductEntity
            {
                Id = product.Id,
                Sku = product.SKU,
                Name = product.Name,
                TypeSymbol = product.Type.Symbol,
                TypeName = product.Type.Name
            };
            _dbContext.Products.Add(entity);
        }

        MapProduct(entity, product, refreshedAtUtc);
        await UpsertBasicFieldsAsync(product, refreshedAtUtc, cancellationToken);
        await SynchronizePricesAsync(product, refreshedAtUtc, cancellationToken);
        await SynchronizeStocksAsync(product, refreshedAtUtc, cancellationToken);
        await SynchronizeSuppliersAsync(product, refreshedAtUtc, cancellationToken);
        await SynchronizeAdvancedFieldsAsync(product, advancedFieldDefinitions, refreshedAtUtc, cancellationToken);
    }

    public async Task DeleteAsync(string sku, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        var product = await _dbContext.Products
            .SingleOrDefaultAsync(product => product.Sku == sku, cancellationToken);

        if (product is null)
            return;

        _dbContext.Products.Remove(product);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void MapProduct(ProductEntity target, ProductDetailsDto source, DateTime refreshedAtUtc)
    {
        target.Sku = source.SKU;
        target.Name = source.Name;
        target.EAN = source.EAN;
        target.TypeSymbol = source.Type.Symbol;
        target.TypeName = source.Type.Name;
        target.IsActive = source.IsActive;
        target.FlagId = source.Flag?.Id;
        target.FlagComment = source.Flag?.Comment;
        target.SupplierLeadTimeDays = source.SupplierLeadTimeDays;
        target.RefreshedAtUtc = refreshedAtUtc;
    }

    private async Task UpsertBasicFieldsAsync(ProductDetailsDto product, DateTime refreshedAtUtc, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.ProductBasicFields.FindAsync([product.Id], cancellationToken);

        if (entity is null)
        {
            entity = new ProductBasicFieldsEntity { ProductId = product.Id };
            _dbContext.ProductBasicFields.Add(entity);
        }

        entity.Field1 = GetBasicValue(product.BasicFields, "PoleWlasne1");
        entity.Field2 = GetBasicValue(product.BasicFields, "PoleWlasne2");
        entity.Field3 = GetBasicValue(product.BasicFields, "PoleWlasne3");
        entity.Field4 = GetBasicValue(product.BasicFields, "PoleWlasne4");
        entity.Field5 = GetBasicValue(product.BasicFields, "PoleWlasne5");
        entity.Field6 = GetBasicValue(product.BasicFields, "PoleWlasne6");
        entity.Field7 = GetBasicValue(product.BasicFields, "PoleWlasne7");
        entity.Field8 = GetBasicValue(product.BasicFields, "PoleWlasne8");
        entity.PresentFieldsMask = GetBasicFieldsMask(product.BasicFields);
        entity.RefreshedAtUtc = refreshedAtUtc;
    }

    private async Task SynchronizePricesAsync(ProductDetailsDto product, DateTime refreshedAtUtc, CancellationToken cancellationToken)
    {
        var existing = (await _dbContext.ProductPrices
                .Where(price => price.ProductId == product.Id)
                .ToListAsync(cancellationToken))
            .ToDictionary(price => price.PriceLevelName, StringComparer.Ordinal);
        var current = product.Prices.ToDictionary(price => price.PriceLevelName, StringComparer.Ordinal);

        foreach (var source in current.Values)
        {
            if (!existing.Remove(source.PriceLevelName, out var target))
            {
                target = new ProductPriceEntity
                {
                    ProductId = product.Id,
                    PriceLevelName = source.PriceLevelName
                };
                _dbContext.ProductPrices.Add(target);
            }

            target.PriceNet = source.PriceNet;
            target.PriceGross = source.PriceGross;
            target.RefreshedAtUtc = refreshedAtUtc;
        }

        _dbContext.ProductPrices.RemoveRange(existing.Values);
    }

    private async Task SynchronizeStocksAsync(ProductDetailsDto product, DateTime refreshedAtUtc, CancellationToken cancellationToken)
    {
        var existing = (await _dbContext.ProductStocks
                .Where(stock => stock.ProductId == product.Id)
                .ToListAsync(cancellationToken))
            .ToDictionary(stock => stock.WarehouseSymbol, StringComparer.Ordinal);
        var current = product.Stocks.ToDictionary(stock => stock.WarehouseSymbol, StringComparer.Ordinal);

        foreach (var source in current.Values)
        {
            if (!existing.Remove(source.WarehouseSymbol, out var target))
            {
                target = new ProductStockEntity
                {
                    ProductId = product.Id,
                    WarehouseSymbol = source.WarehouseSymbol
                };
                _dbContext.ProductStocks.Add(target);
            }

            target.AvailableStock = source.AvailableStock;
            target.ReservedStock = source.ReservedStock;
            target.RefreshedAtUtc = refreshedAtUtc;
        }

        _dbContext.ProductStocks.RemoveRange(existing.Values);
    }

    private async Task SynchronizeSuppliersAsync(ProductDetailsDto product, DateTime refreshedAtUtc, CancellationToken cancellationToken)
    {
        var existing = (await _dbContext.ProductSuppliers
                .Where(supplier => supplier.ProductId == product.Id)
                .ToListAsync(cancellationToken))
            .ToDictionary(supplier => supplier.SupplierId);
        var current = product.DefaultSuppliers.ToDictionary(supplier => supplier.Id);

        foreach (var source in current.Values)
        {
            if (!existing.Remove(source.Id, out var target))
            {
                target = new ProductSupplierEntity
                {
                    ProductId = product.Id,
                    SupplierId = source.Id,
                    Name = source.Name
                };
                _dbContext.ProductSuppliers.Add(target);
            }

            target.Name = source.Name;
            target.Nip = source.Nip;
            target.IsPrimary = source.IsPrimary;
            target.SupplierProductSymbol = source.SupplierProductSymbol;
            target.SupplierProductName = source.SupplierProductName;
            target.RefreshedAtUtc = refreshedAtUtc;
        }

        _dbContext.ProductSuppliers.RemoveRange(existing.Values);
    }

    private async Task SynchronizeAdvancedFieldsAsync(ProductDetailsDto product, IReadOnlyDictionary<string, AdvancedFieldDefinitionDto> definitions, DateTime refreshedAtUtc, CancellationToken cancellationToken)
    {
        var existing = (await _dbContext.ProductAdvancedFields
                .Where(field => field.ProductId == product.Id)
                .ToListAsync(cancellationToken))
            .ToDictionary(field => field.FieldId, StringComparer.Ordinal);
        var current = product.AdvancedFields.ToDictionary(field => field.Id, StringComparer.Ordinal);

        foreach (var source in current.Values)
        {
            if (!definitions.TryGetValue(source.Id, out var definition))
            {
                throw new InvalidOperationException(
                    $"Missing definition for advanced field '{source.Id}'.");
            }

            if (!existing.Remove(source.Id, out var target))
            {
                target = new ProductAdvancedFieldEntity
                {
                    ProductId = product.Id,
                    FieldId = source.Id
                };
                _dbContext.ProductAdvancedFields.Add(target);
            }

            MapAdvancedValue(target, definition, source.Value);
            target.RefreshedAtUtc = refreshedAtUtc;
        }

        _dbContext.ProductAdvancedFields.RemoveRange(existing.Values);
    }

    private static void MapAdvancedValue(ProductAdvancedFieldEntity target, AdvancedFieldDefinitionDto definition, object? value)
    {
        target.DataType = definition.DataType;
        target.TextValue = null;
        target.IntegerValue = null;
        target.DecimalValue = null;
        target.BooleanValue = null;
        target.DateValue = null;
        target.GuidValue = null;
        target.DictionaryIntegerKey = null;
        target.DictionaryGuidKey = null;

        switch (definition.DataType)
        {
            case AdvancedFieldDataType.Text:
            case AdvancedFieldDataType.LongText:
                target.TextValue = GetStringValue(value, definition.Id);
                return;
            case AdvancedFieldDataType.Integer:
                target.IntegerValue = GetStructValue<int>(value, definition.Id);
                return;
            case AdvancedFieldDataType.Decimal:
                target.DecimalValue = GetStructValue<decimal>(value, definition.Id);
                return;
            case AdvancedFieldDataType.Boolean:
                target.BooleanValue = GetStructValue<bool>(value, definition.Id);
                return;
            case AdvancedFieldDataType.Date:
                target.DateValue = GetStructValue<DateTime>(value, definition.Id);
                return;
            case AdvancedFieldDataType.Guid:
                target.GuidValue = GetStructValue<Guid>(value, definition.Id);
                return;
            case AdvancedFieldDataType.Dictionary:
                MapDictionaryValue(target, definition, value);
                return;
            case AdvancedFieldDataType.Unknown:
            default:
                throw new NotSupportedException(
                    $"Unsupported advanced field type for '{definition.Id}': {definition.DataType}.");
        }
    }

    private static void MapDictionaryValue(ProductAdvancedFieldEntity target, AdvancedFieldDefinitionDto definition, object? value)
    {
        var dictionary = definition.Dictionary ?? throw new InvalidOperationException($"Advanced field '{definition.Id}' has no dictionary definition.");
        if (dictionary.KeyType.Equals("int", StringComparison.OrdinalIgnoreCase) || dictionary.KeyType.Equals("System.Int32", StringComparison.Ordinal))
        {
            target.DictionaryIntegerKey = GetStructValue<int>(value, definition.Id);
            return;
        }
        if (dictionary.KeyType.Equals("guid", StringComparison.OrdinalIgnoreCase) || dictionary.KeyType.Equals("System.Guid", StringComparison.Ordinal))
        {
            target.DictionaryGuidKey = GetStructValue<Guid>(value, definition.Id);
            return;
        }
        throw new NotSupportedException($"Unsupported dictionary key type for '{definition.Id}': {dictionary.KeyType}.");
    }

    private static string? GetBasicValue(IReadOnlyCollection<AdditionalFieldValueDto> fields, string id)
    {
        var field = fields.SingleOrDefault(field => field.Id == id);
        return GetStringValue(field?.Value, id);
    }

    private static byte GetBasicFieldsMask(IReadOnlyCollection<AdditionalFieldValueDto> fields)
    {
        byte mask = 0;
        foreach (var field in fields)
        {
            const string prefix = "PoleWlasne";

            if (!field.Id.StartsWith(prefix, StringComparison.Ordinal) || !int.TryParse(field.Id[prefix.Length..], out var fieldNumber) || fieldNumber is < 1 or > 8)
            {
                throw new InvalidOperationException(
                    $"Unsupported basic field id: {field.Id}.");
            }
            mask |= (byte)(1 << (fieldNumber - 1));
        }
        return mask;
    }

    private static string? GetStringValue(object? value, string fieldId)
    {
        if (value is null)
            return null;

        if (value is string text)
            return text;

        if (value is JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined })
            return null;

        if (value is JsonElement { ValueKind: JsonValueKind.String } element)
            return element.GetString();

        throw InvalidFieldValue(fieldId, "a string");
    }

    private static T? GetStructValue<T>(object? value, string fieldId)
        where T : struct
    {
        if (value is null)
            return null;

        if (value is T typedValue)
            return typedValue;

        if (value is JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined })
            return null;

        if (value is JsonElement element)
        {
            try
            {
                return element.Deserialize<T>();
            }
            catch (JsonException exception)
            {
                throw InvalidFieldValue(fieldId, typeof(T).Name, exception);
            }
        }

        throw InvalidFieldValue(fieldId, typeof(T).Name);
    }

    private static InvalidOperationException InvalidFieldValue(
        string fieldId,
        string expectedType,
        Exception? innerException = null) => new(
        $"Additional field '{fieldId}' must contain {expectedType}.",
        innerException);
}
