using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Core.Application.Products;

internal static class ProductMaterializationRequirementMatcher
{
    public static bool Matches(
        ProductDetailsDto product,
        IReadOnlyCollection<MaterializationRequirement> requirements) =>
        requirements.All(requirement => Matches(product, requirement));

    private static bool Matches(
        ProductDetailsDto product,
        MaterializationRequirement requirement) => requirement.FieldId switch
        {
            MaterializationFieldIds.ProductTotalStock => CompareNumeric(
                product.Stocks.Sum(stock => stock.AvailableStock + stock.ReservedStock),
                requirement),
            MaterializationFieldIds.ProductSku => CompareText(product.SKU, requirement),
            MaterializationFieldIds.ProductName => CompareText(product.Name, requirement),
            MaterializationFieldIds.ProductEan => CompareText(product.EAN, requirement),
            _ => throw new NotSupportedException(
                $"Unsupported product materialization field '{requirement.FieldId}'.")
        };

    private static bool CompareNumeric(
        decimal actual,
        MaterializationRequirement requirement)
    {
        var expected = requirement.NumericValue
            ?? throw new InvalidOperationException(
                $"Materialization requirement for '{requirement.FieldId}' has no numeric value.");

        if (requirement.TextValue is not null)
            throw new InvalidOperationException(
                $"Numeric materialization requirement for '{requirement.FieldId}' cannot contain a text value.");

        return requirement.Operator switch
        {
            MaterializationComparisonOperator.Equal => actual == expected,
            MaterializationComparisonOperator.NotEqual => actual != expected,
            MaterializationComparisonOperator.GreaterThan => actual > expected,
            MaterializationComparisonOperator.GreaterThanOrEqual => actual >= expected,
            MaterializationComparisonOperator.LessThan => actual < expected,
            MaterializationComparisonOperator.LessThanOrEqual => actual <= expected,
            _ => throw new NotSupportedException(
                $"Operator '{requirement.Operator}' cannot be used with numeric field '{requirement.FieldId}'.")
        };
    }

    private static bool CompareText(
        string? actual,
        MaterializationRequirement requirement)
    {
        var expected = requirement.TextValue
            ?? throw new InvalidOperationException(
                $"Materialization requirement for '{requirement.FieldId}' has no text value.");

        if (requirement.NumericValue is not null)
            throw new InvalidOperationException(
                $"Text materialization requirement for '{requirement.FieldId}' cannot contain a numeric value.");

        return requirement.Operator switch
        {
            MaterializationComparisonOperator.Equal => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            MaterializationComparisonOperator.NotEqual => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            MaterializationComparisonOperator.Contains => actual?.Contains(expected, StringComparison.OrdinalIgnoreCase) == true,
            MaterializationComparisonOperator.DoesNotContain => actual?.Contains(expected, StringComparison.OrdinalIgnoreCase) != true,
            _ => throw new NotSupportedException(
                $"Operator '{requirement.Operator}' cannot be used with text field '{requirement.FieldId}'.")
        };
    }
}
