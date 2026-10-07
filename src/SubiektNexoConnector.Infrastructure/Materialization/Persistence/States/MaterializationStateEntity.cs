namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class MaterializationStateEntity
{
    public string PolicyKey { get; set; } = string.Empty;
    public int? NextCursor { get; set; }

    public DateTime? CycleStartedAtUtc { get; set; }
    public DateTime? LastBatchCompletedAtUtc { get; set; }
    public DateTime? LastCycleCompletedAtUtc { get; set; }

    public DateTime? LastErrorAtUtc { get; set; }
    public string? LastError { get; set; }
}