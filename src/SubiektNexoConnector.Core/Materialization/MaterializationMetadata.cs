namespace SubiektNexoConnector.Core.Materialization;

public enum MaterializationStatus
{
    Fresh,
    RefreshDue,
    Stale,
    Unavailable
}

public sealed record MaterializationMetadata(MaterializationStatus Status, DateTime? LastSuccessAtUtc)
{
    public static MaterializationMetadata Evaluate(DateTime? lastSuccessAtUtc, TimeSpan lifetime, TimeSpan refreshAhead, DateTime utcNow)
    {
        if (lastSuccessAtUtc is null)
            return new MaterializationMetadata(MaterializationStatus.Unavailable, null);

        if (utcNow >= lastSuccessAtUtc.Value + lifetime)
            return new MaterializationMetadata(MaterializationStatus.Stale, lastSuccessAtUtc);

        if (utcNow >= lastSuccessAtUtc.Value + lifetime - refreshAhead)
            return new MaterializationMetadata(MaterializationStatus.RefreshDue, lastSuccessAtUtc);

        return new MaterializationMetadata(MaterializationStatus.Fresh, lastSuccessAtUtc);
    }
}

public sealed record MaterializedResult<T>(T Value, MaterializationMetadata Metadata);
