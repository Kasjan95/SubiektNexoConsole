namespace SubiektNexoConnector.Core.Materialization;

public static class MaterializationPolicyDefinitions
{
    public static readonly MaterializationPolicyDefinition ProductsHot = new(
        "products.hot",
        "Product details",
        200,
        180,
        15,
        50,
        MaterializationCandidateSelection.ReadModel,
        [
            new MaterializationRequirement(
                MaterializationFieldIds.ProductTotalStock,
                MaterializationComparisonOperator.GreaterThan,
                NumericValue: 0)
        ]);

    public static readonly MaterializationPolicyDefinition ProductsCold = new(
        "products.cold",
        "Product batch refresh",
        100,
        1800,
        90,
        500,
        MaterializationCandidateSelection.ReadModel,
        [
            new MaterializationRequirement(
                MaterializationFieldIds.ProductTotalStock,
                MaterializationComparisonOperator.LessThanOrEqual,
                NumericValue: 0)
        ]);

    public static readonly MaterializationPolicyDefinition ProductsNew = new(
        "products.new",
        "New products discovery",
        300,
        1800,
        90,
        500,
        MaterializationCandidateSelection.MissingFromReadModel,
        []);

    public static IReadOnlyCollection<MaterializationPolicyDefinition> ProductReadModelPolicies { get; } =
        [ProductsHot, ProductsCold];

    public static IReadOnlyCollection<MaterializationPolicyDefinition> ProductWorkerPolicies { get; } =
        [ProductsNew, ProductsHot, ProductsCold];
}
