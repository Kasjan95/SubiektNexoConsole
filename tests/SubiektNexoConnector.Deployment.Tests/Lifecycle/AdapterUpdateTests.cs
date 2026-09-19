using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Xml.Linq;
using SubiektNexoConnector.Deployment.Lifecycle;
using SubiektNexoConnector.Deployment.Packaging;
using SubiektNexoConnector.Deployment.Testing;

namespace SubiektNexoConnector.Deployment.Tests.Lifecycle;

public sealed class AdapterUpdateTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "connector-update-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Pilot-1.0.0.2", "Pilot-1.2.0.0")]
    [InlineData("SubiektNexoConnector.v2.pilot_h01-1.1.0.0", "SubiektNexoConnector.v2.pilot_h01-1.2.0.0")]
    public void UpdatePreservesPackageFamily(string oldIdentity, string expected)
        => Assert.Equal(expected, AdapterUpdatePolicy.NextIdentity(oldIdentity, new Version(1, 2, 0, 0)));

    [Theory]
    [InlineData("1.2.0.0")]
    [InlineData("1.1.9.0")]
    public void SameVersionAndDowngradeAreRejected(string target)
        => Assert.Throws<InvalidOperationException>(() => AdapterUpdatePolicy.NextIdentity("Pilot-1.2.0.0", Version.Parse(target)));

    [Theory]
    [InlineData("Pilot")]
    [InlineData("../Pilot-1.0.0.0")]
    [InlineData("-1.0.0.0")]
    public void InvalidPackageIdentityIsRejected(string identity)
        => Assert.Throws<InvalidDataException>(() => AdapterUpdatePolicy.NextIdentity(identity, new Version(1, 2, 0, 0)));

    [Fact]
    public void BuiltUpdateUsesNewVersionWithOriginalFamilyAndScenario()
    {
        var publish = Path.Combine(_root, "publish"); Directory.CreateDirectory(publish);
        File.WriteAllText(Path.Combine(publish, MpkgBuilder.AdapterExecutableName), "test-payload");
        File.WriteAllText(Path.Combine(publish, "LICENSE"), "test-license");
        File.WriteAllText(Path.Combine(publish, "THIRD-PARTY-NOTICES.txt"), "test third-party notices");
        File.WriteAllText(Path.Combine(publish, "SubiektNexoConnector.Api.runtimeconfig.json"), "{}");
        File.WriteAllText(Path.Combine(publish, "SubiektNexoConnector.Api.dll"), "test-library");
        File.WriteAllText(Path.Combine(publish, "SubiektNexoConnector.Api.deps.json"), """
            {"runtimeTarget":{"name":"test"},"targets":{"test":{"Adapter/1.2.0":{"runtime":{"SubiektNexoConnector.Api.dll":{}}}}},"libraries":{"Adapter/1.2.0":{"type":"project"}}}
            """);
        var result = new MpkgBuilder().Build(new MpkgBuildRequest(publish, Path.Combine(_root, "update.mpkg"),
            "Pilot", new Version(1, 2, 0, 0), "60,*", ExistingPackageIdentity: "Pilot-1.0.0.2"));
        using var zip = ZipFile.OpenRead(result.OutputPath);
        using var input = zip.GetEntry("manifest.xml")!.Open();
        var manifest = XDocument.Load(input);
        Assert.Equal("Pilot-1.2.0.0", manifest.Root!.Attribute("Identity")!.Value);
        var scenario = manifest.Descendants("LaunchScenario").Single();
        Assert.Equal("SubiektNexoConnector.Pilot", scenario.Attribute("Name")!.Value);
        Assert.Contains("--instance Pilot", scenario.Element("LaunchAction")!.Attribute("CommandLineArguments")!.Value);
    }

    private static DeploymentState State() => new()
    {
        Instance = "Pilot", Server = "server", Database = "db", AccountSid = "S-1-5-21-1", Port = 15001,
        RemoteAddresses = "192.168.1.20", SeqEnabled = true, PackageIdentity = "Pilot-1.0.0.2", AdapterVersion = "1.0.0.2",
        PackagePath = "old.mpkg", PackageSha256 = "old-hash", Mode = "update",
        PendingUpdate = new() { Version = "1.2.0.0", PackageIdentity = "Pilot-1.2.0.0", PackagePath = "new.mpkg", Sha256 = "new-hash", InformationalVersion = "1.2.0+commit" }
    };

    [Fact]
    public void CommitRequiresDeploymentAndPassingTest()
    {
        var state = State();
        Assert.Throws<InvalidOperationException>(() => AdapterUpdatePolicy.CommitVerified(state));
        state.PendingUpdate!.Deployed = true;
        state.Steps = [new() { Id = "update-smoke-test", Status = StepStatus.Completed },
            new() { Id = "update-version-test", Status = StepStatus.Failed }];
        Assert.Throws<InvalidOperationException>(() => AdapterUpdatePolicy.CommitVerified(state));
        Assert.Equal("1.0.0.2", state.AdapterVersion);
    }

    [Fact]
    public void CommitChangesOnlyVerifiedPackageMetadata()
    {
        var state = State(); state.PendingUpdate!.Deployed = true;
        state.Steps = [new() { Id = "update-smoke-test", Status = StepStatus.Completed },
            new() { Id = "update-version-test", Status = StepStatus.Completed }];
        AdapterUpdatePolicy.CommitVerified(state);
        Assert.Equal("1.2.0.0", state.AdapterVersion);
        Assert.Equal("new.mpkg", state.PackagePath);
        Assert.Equal("new-hash", state.PackageSha256);
        Assert.Equal("db", state.Database);
        Assert.Equal("S-1-5-21-1", state.AccountSid);
        Assert.Equal("192.168.1.20", state.RemoteAddresses);
        Assert.True(state.SeqEnabled);
        Assert.Equal(15001, state.Port);
    }

    [Fact]
    public void FailedUpdateThatWasDeployed_CanResolveBothPackagesForUninstall()
    {
        var state = State();
        state.PendingUpdate!.Deployed = true;
        state.Steps = [new() { Id = "update-package", Status = StepStatus.Completed },
            new() { Id = "update-test", Status = StepStatus.Failed }];

        var packages = AdapterUpdatePolicy.GetDetachmentPackages(state);

        Assert.Collection(packages,
            current => { Assert.Equal("new.mpkg", current.PackagePath); Assert.True(current.Required); },
            previous => { Assert.Equal("old.mpkg", previous.PackagePath); Assert.True(previous.Required); });
    }

    [Fact]
    public void UpdateThatFailedBeforeDeploy_UsesOnlyPreviouslyVerifiedPackageForUninstall()
    {
        var state = State();
        state.Steps = [new() { Id = "update-backup", Status = StepStatus.Failed }];

        var package = Assert.Single(AdapterUpdatePolicy.GetDetachmentPackages(state));

        Assert.Equal("old.mpkg", package.PackagePath);
        Assert.True(package.Required);
    }

    [Fact]
    public void UncertainDeploy_AlsoChecksPendingPackageWithoutMakingMissingCacheFatal()
    {
        var state = State();
        state.Steps = [new() { Id = "update-package", Status = StepStatus.Failed }];

        var packages = AdapterUpdatePolicy.GetDetachmentPackages(state);

        Assert.Equal("new.mpkg", packages[0].PackagePath);
        Assert.False(packages[0].Required);
        Assert.Equal("old.mpkg", packages[1].PackagePath);
    }

    [Fact]
    public async Task FailedTestPersistsPendingTargetWithoutDeclaringNewVersionReady()
    {
        var state = State(); var store = new DeploymentStore(_root);
        await Assert.ThrowsAsync<IOException>(() => new DeploymentOrchestrator(store).RunAsync(state,
        [new("update-package", "deploy", _ => { state.PendingUpdate!.Deployed = true; return Task.CompletedTask; }),
         new("update-smoke-test", "test", _ => Task.CompletedTask),
         new("update-version-test", "version", _ => throw new IOException("test failed")),
         new("update-commit", "commit", _ => { AdapterUpdatePolicy.CommitVerified(state); return Task.CompletedTask; })]));
        var restored = store.Load()!;
        Assert.Equal(OperationStatus.NeedsRepair, restored.Status);
        Assert.Equal("1.0.0.2", restored.AdapterVersion);
        Assert.Equal("1.2.0.0", restored.PendingUpdate!.Version);
        Assert.True(restored.PendingUpdate.Deployed);
        Assert.Equal(StepStatus.Completed, restored.Steps.Single(s => s.Id == "update-smoke-test").Status);
        Assert.Equal(StepStatus.Failed, restored.Steps.Single(s => s.Id == "update-version-test").Status);
        Assert.DoesNotContain(restored.Steps, s => s.Id == "update-commit");
    }

    [Theory]
    [InlineData("Pilot", "1.2.0.0", "1.2.0+commit", true)]
    [InlineData("Pilot", "1.1.0.0", "1.1.0+commit", false)]
    [InlineData("Pilot", "1.2.0.0", "1.2.0+different", false)]
    [InlineData("Other", "1.2.0.0", "1.2.0+commit", false)]
    [InlineData("Pilot", null, null, false)]
    public async Task RunningProcessMustProveTargetVersionAndRevision(string instance, string? version, string? revision, bool accepted)
    {
        using var client = new HttpClient(new Handler(request =>
        {
            Assert.Equal("TEST-KEY", request.Headers.GetValues("X-Api-Key").Single());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(
                new { instance, version, informationalVersion = revision })) };
        }));
        var operation = new AdapterControl(client).VerifyVersionAsync(15001, "TEST-KEY", "Pilot", "1.2.0.0", "1.2.0+commit", default);
        if (accepted) await operation;
        else await Assert.ThrowsAsync<InvalidOperationException>(() => operation);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
