namespace SubiektNexoConnector.Core.Materialization;

public sealed record MaterializationPolicyDefinition(
    string Id,
    string Name,
    int Priority,
    int LifetimeSeconds,
    int RefreshAheadSeconds,
    int BatchSize,
    MaterializationCandidateSelection CandidateSelection,
    IReadOnlyCollection<MaterializationRequirement> Requirements)
{
    public TimeSpan Lifetime => TimeSpan.FromSeconds(LifetimeSeconds);
    public TimeSpan RefreshAhead => TimeSpan.FromSeconds(RefreshAheadSeconds);
}
