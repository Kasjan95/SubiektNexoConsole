namespace SubiektNexoConnector.Core.Materialization;

public sealed record MaterializationCheckpoint(
    string PolicyKey,
    int? NextCursor,
    DateTime? CycleStartedAtUtc,
    DateTime? LastBatchCompletedAtUtc,
    DateTime? LastCycleCompletedAtUtc,
    DateTime? LastErrorAtUtc,
    string? LastError);