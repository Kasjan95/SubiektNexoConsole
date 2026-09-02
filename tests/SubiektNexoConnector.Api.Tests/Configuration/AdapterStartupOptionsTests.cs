using Microsoft.Extensions.Configuration;
using SubiektNexoConnector.Api.Configuration;
using SubiektNexoConnector.Infrastructure.Configuration;

namespace SubiektNexoConnector.Api.Tests.Configuration;

public sealed class AdapterStartupOptionsTests
{
    [Fact]
    public void Parse_LocalConfiguration_SelectsExplicitConnectionAndFiltersCustomArgument()
    {
        var options = AdapterStartupOptions.Parse(
            [AdapterStartupOptions.LocalConfigurationArgument, "--urls", "http://localhost:5050"]);

        Assert.Equal(AdapterStartupMode.LocalConfiguration, options.Mode);
        Assert.True(options.UseLocalNexoConnection);
        Assert.Null(options.InstanceName);
        Assert.Equal(["--urls", "http://localhost:5050"], options.HostArguments);
    }

    [Fact]
    public void Parse_InsLauncher_SelectsInstanceAndFiltersLauncherArguments()
    {
        var options = AdapterStartupOptions.Parse(
            [
                AdapterStartupOptions.InsLauncherArgument,
                AdapterStartupOptions.InstanceArgument,
                "krakow-01"
            ]);

        Assert.Equal(AdapterStartupMode.InsLauncher, options.Mode);
        Assert.False(options.UseLocalNexoConnection);
        Assert.Equal("krakow-01", options.InstanceName);
        Assert.Empty(options.HostArguments);
    }

    [Fact]
    public void ResolveAdapterInstance_InsLauncher_UsesCommandLineInstance()
    {
        var options = AdapterStartupOptions.Parse(
            [
                AdapterStartupOptions.InsLauncherArgument,
                AdapterStartupOptions.InstanceArgument,
                "krakow-01"
            ]);

        var result = options.ResolveAdapterInstance("incorrect-config-value");

        Assert.Equal("krakow-01", result);
    }

    [Fact]
    public void ResolveAdapterInstance_LocalConfiguration_KeepsConfiguredInstance()
    {
        var options = AdapterStartupOptions.Parse([AdapterStartupOptions.LocalConfigurationArgument]);

        var result = options.ResolveAdapterInstance("local-development");

        Assert.Equal("local-development", result);
    }

    [Fact]
    public void Parse_InsLauncherWithoutInstance_ThrowsClearError()
    {
        var action = () => AdapterStartupOptions.Parse([AdapterStartupOptions.InsLauncherArgument]);

        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Contains(AdapterStartupOptions.InstanceArgument, exception.Message);
    }

    [Fact]
    public void Parse_LocalConfigurationCombinedWithInsLauncher_ThrowsClearError()
    {
        var action = () => AdapterStartupOptions.Parse(
            [
                AdapterStartupOptions.LocalConfigurationArgument,
                AdapterStartupOptions.InsLauncherArgument,
                $"{AdapterStartupOptions.InstanceArgument}=krakow-01"
            ]);

        Assert.Throws<InvalidOperationException>(action);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../krakow")]
    [InlineData("krakow/01")]
    [InlineData("krakow 01")]
    [InlineData("kraków-01")]
    public void GetInstanceSettingsPath_InvalidInstanceName_IsRejected(string instanceName)
    {
        var action = () => AdapterStartupOptions.GetInstanceSettingsPath(instanceName, "C:\\ProgramData");

        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void AddInstanceConfiguration_LoadsInstanceSettingsAndKeepsHostArgumentsOnTop()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"adapter-startup-{Guid.NewGuid():N}");
        var settingsPath = AdapterStartupOptions.GetInstanceSettingsPath("krakow-01", rootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, """
            {
              "Urls": "http://0.0.0.0:15001",
              "Observability": {
                "AdapterInstance": "krakow-01"
              }
            }
            """);

        try
        {
            var options = AdapterStartupOptions.Parse(
                [
                    AdapterStartupOptions.InsLauncherArgument,
                    $"{AdapterStartupOptions.InstanceArgument}=krakow-01",
                    "--urls",
                    "http://localhost:16001"
                ]);
            var configuration = new ConfigurationManager();

            var loadedPath = options.AddInstanceConfiguration(configuration, rootPath);

            Assert.Equal(settingsPath, loadedPath);
            Assert.Equal("krakow-01", configuration["Observability:AdapterInstance"]);
            Assert.Equal("http://localhost:16001", configuration["urls"]);
            AdapterUrlValidator.Validate(configuration[AdapterUrlValidator.ConfigurationKey]);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Theory]
    [InlineData("http://0.0.0.0:15001")]
    [InlineData("https://nexoadapter.firma.local:15002")]
    [InlineData("http://0.0.0.0:15001;https://nexoadapter.firma.local:15002")]
    public void ValidateUrls_ValidExplicitEndpoints_DoesNotThrow(string urls)
    {
        AdapterUrlValidator.Validate(urls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://0.0.0.0")]
    [InlineData("http://0.0.0.0:80")]
    [InlineData("ftp://0.0.0.0:15001")]
    [InlineData("http://user@0.0.0.0:15001")]
    [InlineData("http://0.0.0.0:15001/api")]
    [InlineData("http://0.0.0.0:15001?test=true")]
    public void ValidateUrls_InvalidEndpoint_ThrowsClearError(string? urls)
    {
        var action = () => AdapterUrlValidator.Validate(urls);

        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Contains(AdapterUrlValidator.ConfigurationKey, exception.Message);
        Assert.Contains(AdapterUrlValidator.ExampleUrl, exception.Message);
    }

    [Fact]
    public void Bind_LauncherConfiguration_DoesNotRequireDatabaseCredentials()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Nexo:SystemLogin:NexoUser"] = "operator",
                ["Nexo:SystemLogin:NexoPassword"] = "secret",
                ["Nexo:SferaExecution:QueueTimeoutSeconds"] = "30",
                ["Nexo:SferaExecution:RetryAfterSeconds"] = "5"
            })
            .Build();

        var result = AppConfigBinder.Bind(configuration, requireDatabaseSettings: false);

        Assert.Equal("operator", result.SystemLogin.NexoUser);
        Assert.Equal(string.Empty, result.Database.SqlServer);
    }

    [Fact]
    public void Bind_LocalConfiguration_RequiresDatabaseCredentials()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Nexo:SystemLogin:NexoUser"] = "operator",
                ["Nexo:SystemLogin:NexoPassword"] = "secret"
            })
            .Build();

        var action = () => AppConfigBinder.Bind(configuration, requireDatabaseSettings: true);

        Assert.Throws<SubiektNexoConnector.Infrastructure.Configuration.ConfigurationException>(action);
    }
}
