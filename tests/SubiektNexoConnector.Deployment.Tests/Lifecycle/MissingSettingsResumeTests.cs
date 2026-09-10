using System.Text.Json;
using SubiektNexoConnector.Deployment.Configuration;
using SubiektNexoConnector.Deployment.Lifecycle;

namespace SubiektNexoConnector.Deployment.Tests.Lifecycle;

public sealed class MissingSettingsResumeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "connector-resume-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingSettingsCanBeOpenedForEditing(bool directoryExists)
    {
        var path = InstanceSettingsWriter.GetSettingsPath("test", _root);
        if (directoryExists) Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        Assert.Null(new InstanceSettingsWriter().ReadForEdit("test", _root));
        Assert.False(File.Exists(path)); // Opening repair must not create a config with guessed credentials.
    }

    [Theory]
    [InlineData("broken-json")]
    [InlineData("null")]
    [InlineData("[]")]
    public void CorruptSettingsAreNotTreatedAsMissing(string contents)
    {
        var path = InstanceSettingsWriter.GetSettingsPath("test", _root);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        Assert.ThrowsAny<Exception>(() => new InstanceSettingsWriter().ReadForEdit("test", _root));
        Assert.Equal(contents, File.ReadAllText(path));
    }

    [Fact]
    public async Task ExistingStateWithoutSettingsRequiresPasswordBeforePreparingNexo()
    {
        var instance = "resume-" + Guid.NewGuid().ToString("N");
        var request = new DeploymentRequest { Instance = instance, Server = "server", Port = 15001, NexoUser = "operator" };
        await using var service = new DeploymentService();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PrepareAsync(request, true, null, null,
            new DeploymentState { Instance = instance, Server = "server", Database = "Pilot" }));

        Assert.Contains("hasło operatora Nexo", error.Message);
        Assert.Null(service.State);
        Assert.Throws<InvalidOperationException>(() => service.UpdateRequest(request));
    }

    [Fact]
    public async Task InterruptedBeforeSettingsCanResumeWithReenteredCredentials()
    {
        var store = new DeploymentStore(Path.Combine(_root, "maintenance"));
        var original = new DeploymentState { Instance = "test", Server = "server", Database = "Pilot", Port = 15001, PackageInstalled = true };
        var engine = new DeploymentOrchestrator(store);
        await Assert.ThrowsAsync<IOException>(() => engine.RunAsync(original,
            [new("settings", "Konfiguracja", _ => throw new IOException("interrupted"))]));

        var resumed = store.Load()!;
        var writer = new InstanceSettingsWriter();
        Assert.Null(writer.ReadForEdit(resumed.Instance, _root));
        var request = new DeploymentRequest
        {
            Instance = resumed.Instance, Server = resumed.Server, Port = resumed.Port,
            NexoUser = "operator", NexoPassword = "reentered-password", SeqEnabled = false
        };
        request.Validate(false);
        await engine.RunAsync(resumed, [new("settings", "Konfiguracja", _ =>
        {
            writer.Write(new InstanceSettingsRequest(request.Instance, request.Port, "TEST-KEY",
                request.NexoUser, request.NexoPassword, "", resumed.Database), _root);
            return Task.CompletedTask;
        })]);

        var settings = writer.ReadForEdit(resumed.Instance, _root)!;
        Assert.Equal("operator", settings["Nexo"]!["SystemLogin"]!["NexoUser"]!.GetValue<string>());
        Assert.Equal("reentered-password", settings["Nexo"]!["SystemLogin"]!["NexoPassword"]!.GetValue<string>());
        Assert.Equal("Pilot", settings["Observability"]!["NexoCompany"]!.GetValue<string>());
        Assert.Equal(15001, writer.ReadExisting("test", _root)!.Port);
        Assert.True(resumed.PackageInstalled);
        Assert.Equal(StepStatus.Completed, store.Load()!.Steps.Single().Status);
        Assert.DoesNotContain("reentered-password", JsonSerializer.Serialize(store.Load()));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
