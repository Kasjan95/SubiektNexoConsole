using Microsoft.EntityFrameworkCore;
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Common;
using SubiektNexoConnector.Core.Application.Products;
using SubiektNexoConnector.Infrastructure.Materialization.Persistence;

namespace SubiektNexoConnector.Infrastructure.Materialization.Products;

internal sealed class ProductReadModelReader : IProductReadModelReader
{
    private readonly ReadModelDbContext _dbContext;

    public ProductReadModelReader(ReadModelDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public async Task<IReadOnlyCollection<ProductBasicDto>> GetAllAsync(GetProductsQuery query, CancellationToken cancellationToken = default)
    {
        var products = _dbContext.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            products = products.Where(product => product.Sku.Contains(search) || product.Name.Contains(search) || product.EAN != null && product.EAN.Contains(search));
        }

        return await products
            .OrderBy(product => product.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(
            product => new ProductBasicDto(
                product.Id,
                product.Sku,
                product.Name,
                product.EAN))
            .ToListAsync(cancellationToken);
    }
    public async Task<MaterializedProductDetails?> GetDetailsAsync(string sku,CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .AsNoTracking()
            .AsSplitQuery()
            .Include(product => product.BasicFields)
            .Include(product => product.AdvancedFields)
            .Include(product => product.Prices)
            .Include(product => product.Stocks)
            .Include(product => product.Suppliers)
            .SingleOrDefaultAsync(
                product => product.Sku == sku,
                cancellationToken);

        return product is null
            ? null
            : new MaterializedProductDetails(MapDetails(product), product.RefreshedAtUtc);
    }

    private static ProductDetailsDto MapDetails(ProductEntity product)
    {
        var basicFields = product.BasicFields
            ?? throw new InvalidOperationException(
                $"Product '{product.Sku}' has no materialized basic fields.");

        return new ProductDetailsDto(
            product.Id,
            product.Sku,
            product.Name,
            product.EAN,
            new ProductTypeDto(product.TypeSymbol, product.TypeName),
            product.IsActive,
            product.FlagId is int flagId
                ? new FlagAssignmentDto(flagId, product.FlagComment)
                : null,
            product.SupplierLeadTimeDays,
            MapBasicFields(basicFields),
            MapAdvancedFields(product.AdvancedFields),
            product.Suppliers.OrderByDescending(supplier => supplier.IsPrimary).ThenBy(supplier => supplier.SupplierId).Select(MapSupplier).ToArray(),
            product.Prices.OrderBy(price => price.PriceLevelName, StringComparer.Ordinal).Select(MapPrice).ToArray(),
            product.Stocks.OrderBy(stock => stock.WarehouseSymbol, StringComparer.Ordinal).Select(MapStock).ToArray());
    }

    private static IReadOnlyCollection<AdditionalFieldValueDto> MapBasicFields(
        ProductBasicFieldsEntity fields)
    {
        var values = new[]
        {
            fields.Field1,
            fields.Field2,
            fields.Field3,
            fields.Field4,
            fields.Field5,
            fields.Field6,
            fields.Field7,
            fields.Field8
        };

        return values
            .Select((value, index) => new { Value = value, Index = index })
            .Where(field => (fields.PresentFieldsMask & (1 << field.Index)) != 0)
            .Select(field => new AdditionalFieldValueDto(
                $"PoleWlasne{field.Index + 1}",
                field.Value))
            .ToArray();
    }

    private static IReadOnlyCollection<AdditionalFieldValueDto> MapAdvancedFields(
        IEnumerable<ProductAdvancedFieldEntity> fields) => fields
        .OrderBy(field => field.FieldId, StringComparer.Ordinal)
        .Select(field => new AdditionalFieldValueDto(
            field.FieldId,
            GetAdvancedFieldValue(field)))
        .ToArray();

    private static object? GetAdvancedFieldValue(ProductAdvancedFieldEntity field) =>
        field.DataType switch
        {
            AdvancedFieldDataType.Text => field.TextValue,
            AdvancedFieldDataType.LongText => field.TextValue,
            AdvancedFieldDataType.Integer => field.IntegerValue,
            AdvancedFieldDataType.Decimal => field.DecimalValue,
            AdvancedFieldDataType.Boolean => field.BooleanValue,
            AdvancedFieldDataType.Date => field.DateValue,
            AdvancedFieldDataType.Guid => field.GuidValue,
            AdvancedFieldDataType.Dictionary => GetDictionaryValue(field),
            _ => throw new NotSupportedException(
                $"Unsupported advanced field type for '{field.FieldId}': {field.DataType}.")
        };

    private static object? GetDictionaryValue(ProductAdvancedFieldEntity field)
    {
        if (field.DictionaryIntegerKey.HasValue && field.DictionaryGuidKey.HasValue)
        {
            throw new InvalidOperationException(
                $"Advanced dictionary field '{field.FieldId}' contains both integer and GUID keys.");
        }

        return field.DictionaryIntegerKey.HasValue
            ? field.DictionaryIntegerKey.Value
            : field.DictionaryGuidKey;
    }

    private static ProductSupplierDto MapSupplier(ProductSupplierEntity supplier) => new(
        supplier.SupplierId,
        supplier.Name,
        supplier.Nip,
        supplier.IsPrimary,
        supplier.SupplierProductSymbol,
        supplier.SupplierProductName);

    private static ProductPriceDto MapPrice(ProductPriceEntity price) => new(
        price.PriceLevelName,
        price.PriceNet,
        price.PriceGross);

    private static ProductStockDto MapStock(ProductStockEntity stock) => new(
        stock.WarehouseSymbol,
        stock.AvailableStock,
        stock.ReservedStock);
}
