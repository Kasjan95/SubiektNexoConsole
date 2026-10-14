using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;

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

    public static readonly MaterializationPolicyDefinition Parties = new(
        "parties.all",
        "Party details",
        150,
        1800,
        90,
        100,
        MaterializationCandidateSelection.SourceSystem,
        []);

    public static readonly MaterializationPolicyDefinition AdvancedFieldDefinitionsProduct = new(
        "additional-field-definitions.advanced.product",
        "Product advanced field definitions",
        400,
        2700,
        120,
        1,
        MaterializationCandidateSelection.ReadModel,
        []);

    public static readonly MaterializationPolicyDefinition AdvancedFieldDefinitionsParty = new(
        "additional-field-definitions.advanced.party",
        "Party advanced field definitions",
        390,
        2700,
        120,
        1,
        MaterializationCandidateSelection.ReadModel,
        []);

    public static IReadOnlyCollection<MaterializationPolicyDefinition> ProductReadModelPolicies { get; } =
        [ProductsHot, ProductsCold];

    public static IReadOnlyCollection<MaterializationPolicyDefinition> ProductWorkerPolicies { get; } =
        [ProductsNew, ProductsHot, ProductsCold];

    public static IReadOnlyCollection<MaterializationPolicyDefinition> AdvancedFieldDefinitionsWorkerPolicies { get; } =
        [AdvancedFieldDefinitionsProduct, AdvancedFieldDefinitionsParty];

    public static MaterializationPolicyDefinition GetAdvancedFieldDefinitionsPolicy(AdditionalFieldTarget target) => target switch
    {
        AdditionalFieldTarget.Product => AdvancedFieldDefinitionsProduct,
        AdditionalFieldTarget.Party => AdvancedFieldDefinitionsParty,
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported advanced field target.")
    };

    public static AdditionalFieldTarget GetAdvancedFieldDefinitionsTarget(string policyId)
    {
        if (policyId == AdvancedFieldDefinitionsProduct.Id)
            return AdditionalFieldTarget.Product;
        if (policyId == AdvancedFieldDefinitionsParty.Id)
            return AdditionalFieldTarget.Party;
        throw new ArgumentOutOfRangeException(nameof(policyId), policyId, "Unknown advanced field definitions policy.");
    }
}
