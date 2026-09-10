using System.Text.Json.Nodes;
using SubiektNexoConnector.Deployment.Configuration;
using SubiektNexoConnector.Deployment.Lifecycle;
using SubiektNexoConnector.Deployment.Packaging;
using SubiektNexoConnector.Deployment.Windows;

namespace SubiektNexoConnector.Deployment.Tests.Lifecycle;

public sealed class DeploymentLifecycleTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "connector-lifecycle-" + Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task FailureIsPersisted_AndLaterStepsDoNotRun()
    {
        var store = new DeploymentStore(_root);
        var state = new DeploymentState { Instance = "test" };
        var laterCalled = false;
        await Assert.ThrowsAsync<IOException>(() => new DeploymentOrchestrator(store).RunAsync(state,
        [new("first", "first", _ => Task.CompletedTask), new("fail", "fail", _ => throw new IOException("secret-from-provider")),
         new("later", "later", _ => { laterCalled = true; return Task.CompletedTask; })]));
        var saved = store.Load()!;
        Assert.Equal(OperationStatus.NeedsRepair, saved.Status);
        Assert.Equal(StepStatus.Completed, saved.Steps[0].Status);
        Assert.Equal(StepStatus.Failed, saved.Steps[1].Status);
        Assert.False(laterCalled);
        Assert.DoesNotContain("secret-from-provider", File.ReadAllText(Path.Combine(_root, "deployment.json")));
    }
    [Fact]
    public async Task ResumeVerifiesResources_AndRepairsMissingCompletedStep()
    {
        var store = new DeploymentStore(_root);
        var state = new DeploymentState { Instance = "test", Steps = [new() { Id = "file", Status = StepStatus.Completed }] };
        var exists = false;
        var executions = 0;
        var step = new DeploymentStep("file", "file", _ => { exists = true; executions++; return Task.CompletedTask; }, _ => Task.FromResult(exists));
        await new DeploymentOrchestrator(store).RunAsync(state, [step]);
        await new DeploymentOrchestrator(store).RunAsync(state, [step]);
        Assert.Equal(1, executions);
        Assert.Equal(OperationStatus.Ready, store.Load()!.Status);
    }
    [Fact]
    public async Task OptionalFailureIsSkipped_RequiredFailureIsNotSuccess()
    {
        var store = new DeploymentStore(_root);
        var state = new DeploymentState { Instance = "test" };
        await new DeploymentOrchestrator(store).RunAsync(state,
            [new("seq", "Seq", _ => throw new HttpRequestException(), Optional: true)]);
        Assert.Equal(OperationStatus.Ready, state.Status);
        Assert.Equal(StepStatus.Skipped, state.Steps.Single().Status);
        await Assert.ThrowsAsync<HttpRequestException>(() => new DeploymentOrchestrator(store).RunAsync(state,
            [new("sfera", "Sfera", _ => throw new HttpRequestException())]));
        Assert.Equal(OperationStatus.NeedsRepair, state.Status);
    }
    [Fact]
    public async Task ExplicitSkipDoesNotExecute_AndCancellationStopsOptionalStep()
    {
        var store = new DeploymentStore(_root);
        var state = new DeploymentState { Instance = "test" };
        await new DeploymentOrchestrator(store).RunAsync(state,
            [new("seq", "Seq", _ => throw new Exception("must not execute"), Skip: true)]);
        using var cancel = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new DeploymentOrchestrator(store).RunAsync(state,
            [new("optional", "optional", token => { cancel.Cancel(); token.ThrowIfCancellationRequested(); return Task.CompletedTask; }, Optional: true)], cancel.Token));
        Assert.Equal(OperationStatus.Cancelled, store.Load()!.Status);
    }
    [Fact]
    public async Task MutationSuccessWithoutVerificationIsNotCompleted()
    {
        var state = new DeploymentState { Instance = "test" };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new DeploymentOrchestrator(new DeploymentStore(_root)).RunAsync(state,
            [new("task", "task", _ => Task.CompletedTask, _ => Task.FromResult(false))]));
        Assert.Equal(StepStatus.Failed, state.Steps.Single().Status);
    }
    [Fact]
    public void NewerStateSchemaIsRejected()
    {
        var store = new DeploymentStore(_root);
        store.Save(new DeploymentState { Instance = "test", SchemaVersion = 99 });
        Assert.Throws<InvalidDataException>(() => store.Load());
    }
    [Theory]
    [InlineData("a--b", "a_b")]
    [InlineData("a_hb", "a-b")]
    [InlineData("a_ub", "a_b")]
    public void PackageNamesDoNotCollide(string first, string second) => Assert.NotEqual(DeploymentNames.GetPackageName(first), DeploymentNames.GetPackageName(second));
    [Theory]
    [InlineData("CON")][InlineData("nul")][InlineData("LPT1")][InlineData("COM9")]
    public void WindowsDeviceNamesAreRejected(string name) => Assert.Throws<InvalidOperationException>(() => DeploymentNames.ValidateInstanceName(name));
    [Fact]
    public void PathsCannotEscapeOrDeleteTheRoot()
    {
        Assert.Throws<IOException>(() => SafePath.Child(_root, Path.Combine(_root, "..", "other")));
        Assert.Throws<IOException>(() => SafePath.Child(_root, _root));
        Assert.Throws<IOException>(() => SafePath.DeleteTree(_root, _root));
    }
    [Theory]
    [InlineData("192.168.1.2,10.0.0.0/24")][InlineData("LocalSubnet")][InlineData("::1,fe80::/64")]
    public void FirewallAcceptsRestrictedRanges(string ranges) => FirewallRuleInstaller.Validate(ranges, "private,domain");
    [Theory]
    [InlineData("*")][InlineData("any")][InlineData("10.0.0.1/33")][InlineData("host.example.com")][InlineData("10.0.0.1,,10.0.0.2")]
    public void FirewallRejectsUncontrolledOrInvalidInput(string ranges) => Assert.Throws<InvalidOperationException>(() => FirewallRuleInstaller.Validate(ranges, "private"));
    [Fact]
    public void DisabledSeqIgnoresItsUnusedValue()
    {
        var request = new DeploymentRequest { Instance = "test", Server = "server", Port = 15001, NexoUser = "operator", NexoPassword = "secret", SeqEnabled = false, SeqUrl = "not a URL" };
        request.Validate(false);
    }
    [Fact]
    public void RepairPreservesCustomSettingsAndApiKey_WhenSeqDisabled()
    {
        var writer = new InstanceSettingsWriter();
        var request = new InstanceSettingsRequest("test", 15001, "KEY", "operator", "secret", "http://localhost:5341", "db");
        var path = writer.Write(request, _root).SettingsPath;
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["Nexo"]!["SferaExecution"]!["QueueTimeoutSeconds"] = 120;
        document["CustomFutureOption"] = "keep";
        File.WriteAllText(path, document.ToJsonString());
        writer.Write(request with { OverwriteExisting = true, SeqUrl = "", Port = 15002 }, _root);
        var updated = JsonNode.Parse(File.ReadAllText(path))!;
        Assert.Equal(120, updated["Nexo"]!["SferaExecution"]!["QueueTimeoutSeconds"]!.GetValue<int>());
        Assert.Equal("keep", updated["CustomFutureOption"]!.GetValue<string>());
        Assert.Equal("KEY", updated["Auth"]!["ApiKey"]!.GetValue<string>());
        Assert.DoesNotContain((JsonArray)updated["Serilog"]!["WriteTo"]!, node => node?["Name"]?.GetValue<string>() == "Seq");
        Assert.True(File.Exists(path + ".backup"));
    }
    [Fact]
    public void CorruptSettingsAreNotOverwritten()
    {
        var request = new InstanceSettingsRequest("test", 15001, "KEY", "operator", "secret", "", "db", true);
        var path = InstanceSettingsWriter.GetSettingsPath("test", _root);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, "broken-json");
        Assert.ThrowsAny<Exception>(() => new InstanceSettingsWriter().Write(request, _root));
        Assert.Equal("broken-json", File.ReadAllText(path));
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
