using System.Text.Json;
using SubiektNexoConnector.Deployment.Configuration;

namespace SubiektNexoConnector.Deployment.Tests.Configuration;

public sealed class InstanceSettingsWriterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"SubiektNexoConnector-settings-{Guid.NewGuid():N}");

    [Fact]
    public void Write_CreatesLauncherModeSettingsForTheDeclaredInstance()
    {
        var request = new InstanceSettingsRequest(
            "laptop-pilot",
            15001,
            "TEST-API-KEY",
            "Szef",
            "test-password",
            "http://seq.local:5341",
            "Pilot");

        var result = new InstanceSettingsWriter().Write(request, _root);

        Assert.Equal(
            Path.Combine(_root, "SubiektNexoConnector", "instances", "laptop-pilot", "settings.json"),
            result.SettingsPath);
        using var document = JsonDocument.Parse(File.ReadAllText(result.SettingsPath));
        var root = document.RootElement;
        Assert.Equal("http://0.0.0.0:15001", root.GetProperty("Urls").GetString());
        Assert.Equal("TEST-API-KEY", root.GetProperty("Auth").GetProperty("ApiKey").GetString());
        Assert.Equal("laptop-pilot", root.GetProperty("Observability").GetProperty("AdapterInstance").GetString());
        Assert.Equal("Szef", root.GetProperty("Nexo").GetProperty("SystemLogin").GetProperty("NexoUser").GetString());
        Assert.Equal(
            Path.Combine(_root, "SubiektNexoConnector", "instances", "laptop-pilot", "data", "idempotency.db"),
            root.GetProperty("Idempotency").GetProperty("DatabasePath").GetString());
        Assert.Equal(
            Path.Combine(_root, "SubiektNexoConnector", "instances", "laptop-pilot", "data", "read-model.db"),
            root.GetProperty("Materialization").GetProperty("DatabasePath").GetString());
        Assert.False(root.GetProperty("Nexo").TryGetProperty("Database", out _));
        Assert.Contains(
            "Serilog.Sinks.File",
            root.GetProperty("Serilog").GetProperty("Using").EnumerateArray().Select(value => value.GetString()));
        Assert.Contains(
            root.GetProperty("Serilog").GetProperty("WriteTo").EnumerateArray(),
            sink => sink.GetProperty("Name").GetString() == "File");
    }

    [Fact]
    public void EnsurePersistenceConfiguration_AddsPathsAndPreservesExistingSettings()
    {
        var settingsPath = InstanceSettingsWriter.GetSettingsPath("pilot-existing", _root);
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, """
            {
              "Auth": { "ApiKey": "KEEP" },
              "CustomFutureOption": "keep"
            }
            """);

        new InstanceSettingsWriter().EnsurePersistenceConfiguration("pilot-existing", _root);

        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
        var root = document.RootElement;
        Assert.Equal("KEEP", root.GetProperty("Auth").GetProperty("ApiKey").GetString());
        Assert.Equal("keep", root.GetProperty("CustomFutureOption").GetString());
        Assert.Equal(
            Path.Combine(_root, "SubiektNexoConnector", "instances", "pilot-existing", "data", "idempotency.db"),
            root.GetProperty("Idempotency").GetProperty("DatabasePath").GetString());
        Assert.Equal(
            Path.Combine(_root, "SubiektNexoConnector", "instances", "pilot-existing", "data", "read-model.db"),
            root.GetProperty("Materialization").GetProperty("DatabasePath").GetString());
    }

    [Fact]
    public void ReadExisting_ReturnsApiKeyAndPort()
    {
        var writer = new InstanceSettingsWriter();
        writer.Write(new InstanceSettingsRequest(
            "pilot-existing", 15123, "EXISTING-KEY", "Szef", "password", string.Empty, "Pilot"), _root);

        var existing = writer.ReadExisting("pilot-existing", _root);

        Assert.NotNull(existing);
        Assert.Equal("EXISTING-KEY", existing.ApiKey);
        Assert.Equal(15123, existing.Port);
    }

    [Fact]
    public void Write_DoesNotOverwriteExistingSettingsWithoutConsent()
    {
        var request = new InstanceSettingsRequest(
            "pilot-01", 15001, "KEY", "Szef", "password", string.Empty, "Pilot");
        var writer = new InstanceSettingsWriter();
        writer.Write(request, _root);

        var action = () => writer.Write(request, _root);

        Assert.Throws<IOException>(action);
    }

    [Fact]
    public void ApiKeyGenerator_CreatesA256BitHexKey()
    {
        var first = ApiKeyGenerator.Generate();
        var second = ApiKeyGenerator.Generate();

        Assert.Equal(64, first.Length);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ApiKeyFileWriter_ExportsTransferableTextFileForInstance()
    {
        var result = new ApiKeyFileWriter().Write(_root, "laptop-pilot", "TEST-KEY");

        Assert.Equal(
            Path.Combine(_root, "SubiektNexoConnector.laptop-pilot-api-key.txt"),
            result.Path);
        var contents = File.ReadAllText(result.Path);
        Assert.Contains("Instance: laptop-pilot", contents);
        Assert.Contains("Header: X-Api-Key", contents);
        Assert.Contains("ApiKey: TEST-KEY", contents);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
