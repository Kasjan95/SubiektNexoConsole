namespace SubiektNexoConnector.Core.Materialization;

public sealed record MaterializationRequirement(
    string FieldId,
    MaterializationComparisonOperator Operator,
    decimal? NumericValue = null,
    string? TextValue = null);
