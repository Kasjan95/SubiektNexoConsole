namespace SubiektNexoConnector.Core.Materialization;
public sealed record MaterializationPolicy(
    string PolicyKey,
    TimeSpan Lifetime,
    DateTime UpdatedAtUtc);