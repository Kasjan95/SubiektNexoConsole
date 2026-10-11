using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Core.Tests.Materialization;

public sealed class MaterializationMetadataTests
{
    private static readonly DateTime UtcNow = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan RefreshAhead = TimeSpan.FromMinutes(2);

    [Fact]
    public void Evaluate_ReturnsUnavailable_WhenResourceWasNeverMaterialized()
    {
        var result = MaterializationMetadata.Evaluate(null, Lifetime, RefreshAhead, UtcNow);

        Assert.Equal(MaterializationStatus.Unavailable, result.Status);
        Assert.Null(result.LastSuccessAtUtc);
    }

    [Fact]
    public void Evaluate_ReturnsFresh_BeforeRefreshAheadWindow()
    {
        var lastSuccessAtUtc = UtcNow - Lifetime + RefreshAhead + TimeSpan.FromTicks(1);

        var result = MaterializationMetadata.Evaluate(lastSuccessAtUtc, Lifetime, RefreshAhead, UtcNow);

        Assert.Equal(MaterializationStatus.Fresh, result.Status);
        Assert.Equal(lastSuccessAtUtc, result.LastSuccessAtUtc);
    }

    [Fact]
    public void Evaluate_ReturnsRefreshDue_AtRefreshAheadBoundary()
    {
        var lastSuccessAtUtc = UtcNow - Lifetime + RefreshAhead;

        var result = MaterializationMetadata.Evaluate(lastSuccessAtUtc, Lifetime, RefreshAhead, UtcNow);

        Assert.Equal(MaterializationStatus.RefreshDue, result.Status);
        Assert.Equal(lastSuccessAtUtc, result.LastSuccessAtUtc);
    }

    [Fact]
    public void Evaluate_ReturnsStale_AtLifetimeBoundary()
    {
        var lastSuccessAtUtc = UtcNow - Lifetime;

        var result = MaterializationMetadata.Evaluate(lastSuccessAtUtc, Lifetime, RefreshAhead, UtcNow);

        Assert.Equal(MaterializationStatus.Stale, result.Status);
        Assert.Equal(lastSuccessAtUtc, result.LastSuccessAtUtc);
    }
}
