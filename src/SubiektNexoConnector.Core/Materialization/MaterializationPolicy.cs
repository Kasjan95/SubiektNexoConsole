namespace SubiektNexoConnector.Core.Materialization;
public sealed record MaterializationPolicy(
    string Id,
    string Name,
    TimeSpan Lifetime,
    TimeSpan RefreshAhead,
    int BatchSize,
    MaterializationCandidateSelection CandidateSelection,
    IReadOnlyCollection<MaterializationRequirement> Requirements,
    DateTime UpdatedAtUtc);
