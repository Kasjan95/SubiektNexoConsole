namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class MaterializationPolicyEntity
{
    public required string PolicyKey { get; set; }
    public int LifetimeSeconds { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}