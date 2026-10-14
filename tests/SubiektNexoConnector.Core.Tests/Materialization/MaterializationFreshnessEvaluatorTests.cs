using NSubstitute;
using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Core.Tests.Materialization;

public sealed class MaterializationFreshnessEvaluatorTests
{
    [Fact]
    public async Task EvaluateAsync_UsesConfiguredPolicyLifetime()
    {
        var definition = MaterializationPolicyDefinitions.Parties;
        var store = Substitute.For<IMaterializationPolicyStore>();
        var refreshedAtUtc = DateTime.UtcNow.AddMinutes(-10);
        store.GetOrCreateAsync(definition, Arg.Any<CancellationToken>()).Returns(new MaterializationPolicy(definition.Id, definition.Name, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), definition.BatchSize, definition.CandidateSelection, definition.Requirements, DateTime.UtcNow));
        var evaluator = new MaterializationFreshnessEvaluator(store);

        var result = await evaluator.EvaluateAsync(definition, refreshedAtUtc);

        Assert.Equal(MaterializationStatus.Stale, result.Status);
        Assert.Equal(refreshedAtUtc, result.LastSuccessAtUtc);
        await store.Received(1).GetOrCreateAsync(definition, Arg.Any<CancellationToken>());
    }
}
