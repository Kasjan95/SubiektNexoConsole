using Microsoft.EntityFrameworkCore;
using SubiektNexoConnector.Core.Application.Products;
using SubiektNexoConnector.Core.Materialization;
using SubiektNexoConnector.Infrastructure.Materialization.Persistence;

namespace SubiektNexoConnector.Infrastructure.Materialization.Products;

internal sealed class ProductMaterializationCandidateStore
    : IProductMaterializationCandidateStore
{
    private readonly ReadModelDbContext _dbContext;

    public ProductMaterializationCandidateStore(ReadModelDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProductMaterializationCandidateBatch> GetDueAsync(
        MaterializationPolicy policy,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (policy.CandidateSelection != MaterializationCandidateSelection.ReadModel)
            throw new ArgumentException(
                $"Policy '{policy.Id}' does not select candidates from the read model.",
                nameof(policy));

        var refreshCutoffUtc = utcNow.Add(policy.RefreshAhead).Subtract(policy.Lifetime);
        var products = _dbContext.Products
            .AsNoTracking()
            .Where(product => product.RefreshedAtUtc <= refreshCutoffUtc);

        foreach (var requirement in policy.Requirements)
            products = ApplyRequirement(products, requirement);

        var skus = await products
            .OrderBy(product => product.RefreshedAtUtc)
            .ThenBy(product => product.Id)
            .Take(checked(policy.BatchSize + 1))
            .Select(product => product.Sku)
            .ToListAsync(cancellationToken);

        return new ProductMaterializationCandidateBatch(
            skus.Take(policy.BatchSize).ToArray(),
            skus.Count > policy.BatchSize);
    }

    public async Task<IReadOnlySet<int>> GetAllIdsAsync(
        CancellationToken cancellationToken = default)
    {
        var existingIds = await _dbContext.Products
            .AsNoTracking()
            .Select(product => product.Id)
            .ToListAsync(cancellationToken);

        return existingIds.ToHashSet();
    }

    private static IQueryable<ProductEntity> ApplyRequirement(
        IQueryable<ProductEntity> products,
        MaterializationRequirement requirement) => requirement.FieldId switch
        {
            MaterializationFieldIds.ProductTotalStock => ApplyTotalStockRequirement(products, requirement),
            MaterializationFieldIds.ProductSku => ApplyTextRequirement(products, requirement, MaterializationFieldIds.ProductSku),
            MaterializationFieldIds.ProductName => ApplyTextRequirement(products, requirement, MaterializationFieldIds.ProductName),
            MaterializationFieldIds.ProductEan => ApplyTextRequirement(products, requirement, MaterializationFieldIds.ProductEan),
            _ => throw new NotSupportedException(
                $"Unsupported product materialization field '{requirement.FieldId}'.")
        };

    private static IQueryable<ProductEntity> ApplyTotalStockRequirement(
        IQueryable<ProductEntity> products,
        MaterializationRequirement requirement)
    {
        var expected = (double)(requirement.NumericValue
            ?? throw new InvalidOperationException(
                $"Materialization requirement for '{requirement.FieldId}' has no numeric value."));

        return requirement.Operator switch
        {
            MaterializationComparisonOperator.Equal => products.Where(product =>
                (product.Stocks.Sum(stock => (double?)stock.AvailableStock + (double?)stock.ReservedStock) ?? 0) == expected),
            MaterializationComparisonOperator.NotEqual => products.Where(product =>
                (product.Stocks.Sum(stock => (double?)stock.AvailableStock + (double?)stock.ReservedStock) ?? 0) != expected),
            MaterializationComparisonOperator.GreaterThan => products.Where(product =>
                (product.Stocks.Sum(stock => (double?)stock.AvailableStock + (double?)stock.ReservedStock) ?? 0) > expected),
            MaterializationComparisonOperator.GreaterThanOrEqual => products.Where(product =>
                (product.Stocks.Sum(stock => (double?)stock.AvailableStock + (double?)stock.ReservedStock) ?? 0) >= expected),
            MaterializationComparisonOperator.LessThan => products.Where(product =>
                (product.Stocks.Sum(stock => (double?)stock.AvailableStock + (double?)stock.ReservedStock) ?? 0) < expected),
            MaterializationComparisonOperator.LessThanOrEqual => products.Where(product =>
                (product.Stocks.Sum(stock => (double?)stock.AvailableStock + (double?)stock.ReservedStock) ?? 0) <= expected),
            _ => throw new NotSupportedException(
                $"Operator '{requirement.Operator}' cannot be used with numeric field '{requirement.FieldId}'.")
        };
    }

    private static IQueryable<ProductEntity> ApplyTextRequirement(
        IQueryable<ProductEntity> products,
        MaterializationRequirement requirement,
        string fieldId)
    {
        var expected = requirement.TextValue
            ?? throw new InvalidOperationException(
                $"Materialization requirement for '{requirement.FieldId}' has no text value.");

        return (fieldId, requirement.Operator) switch
        {
            (MaterializationFieldIds.ProductSku, MaterializationComparisonOperator.Equal) => products.Where(product => product.Sku == expected),
            (MaterializationFieldIds.ProductSku, MaterializationComparisonOperator.NotEqual) => products.Where(product => product.Sku != expected),
            (MaterializationFieldIds.ProductSku, MaterializationComparisonOperator.Contains) => products.Where(product => product.Sku.Contains(expected)),
            (MaterializationFieldIds.ProductSku, MaterializationComparisonOperator.DoesNotContain) => products.Where(product => !product.Sku.Contains(expected)),
            (MaterializationFieldIds.ProductName, MaterializationComparisonOperator.Equal) => products.Where(product => product.Name == expected),
            (MaterializationFieldIds.ProductName, MaterializationComparisonOperator.NotEqual) => products.Where(product => product.Name != expected),
            (MaterializationFieldIds.ProductName, MaterializationComparisonOperator.Contains) => products.Where(product => product.Name.Contains(expected)),
            (MaterializationFieldIds.ProductName, MaterializationComparisonOperator.DoesNotContain) => products.Where(product => !product.Name.Contains(expected)),
            (MaterializationFieldIds.ProductEan, MaterializationComparisonOperator.Equal) => products.Where(product => product.EAN == expected),
            (MaterializationFieldIds.ProductEan, MaterializationComparisonOperator.NotEqual) => products.Where(product => product.EAN != expected),
            (MaterializationFieldIds.ProductEan, MaterializationComparisonOperator.Contains) => products.Where(product => product.EAN != null && product.EAN.Contains(expected)),
            (MaterializationFieldIds.ProductEan, MaterializationComparisonOperator.DoesNotContain) => products.Where(product => product.EAN == null || !product.EAN.Contains(expected)),
            _ => throw new NotSupportedException(
                $"Operator '{requirement.Operator}' cannot be used with text field '{requirement.FieldId}'.")
        };
    }
}
