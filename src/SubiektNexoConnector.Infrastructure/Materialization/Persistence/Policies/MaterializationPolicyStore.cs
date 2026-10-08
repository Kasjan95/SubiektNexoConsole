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
        string policyKey,
        TimeSpan defaultLifetime,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyKey);

        if (defaultLifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(
                nameof(defaultLifetime),
                defaultLifetime,
                "Materialization policy lifetime must be greater than zero.");

        var entity = await _dbContext.MaterializationPolicies.FindAsync(
            [policyKey],
            cancellationToken);

        if (entity is null)
        {
            entity = new MaterializationPolicyEntity
            {
                PolicyKey = policyKey,
                LifetimeSeconds = checked((int)defaultLifetime.TotalSeconds),
                UpdatedAtUtc = DateTime.UtcNow
            };

            _dbContext.MaterializationPolicies.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (entity.LifetimeSeconds <= 0)
            throw new InvalidOperationException(
                $"Materialization policy '{policyKey}' has an invalid lifetime: {entity.LifetimeSeconds} seconds.");

        return new MaterializationPolicy(
            entity.PolicyKey,
            TimeSpan.FromSeconds(entity.LifetimeSeconds),
            entity.UpdatedAtUtc);
    }
}
