using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence.Policies;

internal sealed class MaterializationPolicyStore : IMaterializationPolicyStore
{
    private readonly ReadModelDbContext _dbContext;

    public MaterializationPolicyStore(ReadModelDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MaterializationPolicy> GetOrCreateAsync(
        MaterializationPolicyDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Name);
        ArgumentNullException.ThrowIfNull(definition.Requirements);

        if (definition.LifetimeSeconds <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                definition.LifetimeSeconds,
                "Materialization policy lifetime must be greater than zero.");

        if (definition.RefreshAheadSeconds < 0
            || definition.RefreshAheadSeconds >= definition.LifetimeSeconds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                definition.RefreshAheadSeconds,
                "Refresh ahead must be non-negative and shorter than the policy lifetime.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(definition.BatchSize);
        ValidateCandidateSelection(definition);
        foreach (var requirement in definition.Requirements)
            ValidateRequirement(requirement);

        var entity = await _dbContext.MaterializationPolicies.FindAsync(
            [definition.Id],
            cancellationToken);

        if (entity is null)
        {
            entity = new MaterializationPolicyEntity
            {
                PolicyKey = definition.Id,
                LifetimeSeconds = definition.LifetimeSeconds,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _dbContext.MaterializationPolicies.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (entity.LifetimeSeconds <= 0)
            throw new InvalidOperationException(
                $"Materialization policy '{definition.Id}' has an invalid lifetime: {entity.LifetimeSeconds} seconds.");

        if (definition.RefreshAheadSeconds >= entity.LifetimeSeconds)
            throw new InvalidOperationException(
                $"Materialization policy '{definition.Id}' has a refresh-ahead value that is not shorter than its configured lifetime.");

        return new MaterializationPolicy(
            entity.PolicyKey,
            definition.Name,
            TimeSpan.FromSeconds(entity.LifetimeSeconds),
            definition.RefreshAhead,
            definition.BatchSize,
            definition.CandidateSelection,
            definition.Requirements.ToArray(),
            entity.UpdatedAtUtc);
    }

    private static void ValidateCandidateSelection(
        MaterializationPolicyDefinition definition)
    {
        if (!Enum.IsDefined(definition.CandidateSelection))
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                definition.CandidateSelection,
                "Unknown materialization candidate selection.");

        if (definition.CandidateSelection == MaterializationCandidateSelection.MissingFromReadModel
            && definition.Requirements.Count > 0)
        {
            throw new NotSupportedException(
                "Requirements for source-system discovery are not supported yet.");
        }
    }

    private static void ValidateRequirement(MaterializationRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        ArgumentException.ThrowIfNullOrWhiteSpace(requirement.FieldId);

        if (!Enum.IsDefined(requirement.Operator))
            throw new ArgumentOutOfRangeException(
                nameof(requirement),
                requirement.Operator,
                "Unknown materialization comparison operator.");

        var isNumericField = requirement.FieldId == MaterializationFieldIds.ProductTotalStock;
        var isTextField = requirement.FieldId is MaterializationFieldIds.ProductSku
            or MaterializationFieldIds.ProductName
            or MaterializationFieldIds.ProductEan;

        if (!isNumericField && !isTextField)
            throw new NotSupportedException(
                $"Unsupported materialization field '{requirement.FieldId}'.");

        if (isNumericField
            && (requirement.NumericValue is null || requirement.TextValue is not null))
        {
            throw new InvalidOperationException(
                $"Requirement for numeric field '{requirement.FieldId}' must contain only a numeric value.");
        }

        if (isTextField
            && (requirement.TextValue is null || requirement.NumericValue is not null))
        {
            throw new InvalidOperationException(
                $"Requirement for text field '{requirement.FieldId}' must contain only a text value.");
        }

        var isTextOperator = requirement.Operator is
            MaterializationComparisonOperator.Contains
            or MaterializationComparisonOperator.DoesNotContain;

        if (isNumericField && isTextOperator)
            throw new NotSupportedException(
                $"Operator '{requirement.Operator}' cannot be used with numeric field '{requirement.FieldId}'.");

        if (isTextField
            && requirement.Operator is not (
                MaterializationComparisonOperator.Equal
                or MaterializationComparisonOperator.NotEqual
                or MaterializationComparisonOperator.Contains
                or MaterializationComparisonOperator.DoesNotContain))
        {
            throw new NotSupportedException(
                $"Operator '{requirement.Operator}' cannot be used with text field '{requirement.FieldId}'.");
        }
    }
}
