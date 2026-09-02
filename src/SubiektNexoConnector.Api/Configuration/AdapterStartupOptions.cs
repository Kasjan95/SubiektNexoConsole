using Microsoft.Extensions.Configuration;
using System.IO;

namespace SubiektNexoConnector.Api.Configuration;

public enum AdapterStartupMode
{
    Unspecified,
    LocalConfiguration,
    InsLauncher
}

public sealed class AdapterStartupOptions
{
    public const string LocalConfigurationArgument = "--config";
    public const string InsLauncherArgument = "/UruchomionePrzezInsLauncher";
    public const string InstanceArgument = "--instance";
    public const string SettingsFileName = "settings.json";

    private AdapterStartupOptions(
        AdapterStartupMode mode,
        string? instanceName,
        IReadOnlyList<string> hostArguments)
    {
        Mode = mode;
        InstanceName = instanceName;
        HostArguments = hostArguments;
    }

    public AdapterStartupMode Mode { get; }
    public string? InstanceName { get; }
    public IReadOnlyList<string> HostArguments { get; }
    public bool UseLocalNexoConnection => Mode == AdapterStartupMode.LocalConfiguration;

    public string ResolveAdapterInstance(string configuredInstance)
    {
        return Mode == AdapterStartupMode.InsLauncher
            ? InstanceName!
            : configuredInstance;
    }

    public static AdapterStartupOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var useLocalConfiguration = false;
        var launchedByInsLauncher = false;
        string? instanceName = null;
        var hostArguments = new List<string>();

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];

            if (argument.Equals(LocalConfigurationArgument, StringComparison.OrdinalIgnoreCase))
            {
                useLocalConfiguration = true;
                continue;
            }

            if (argument.Equals(InsLauncherArgument, StringComparison.OrdinalIgnoreCase))
            {
                launchedByInsLauncher = true;
                continue;
            }

            if (argument.StartsWith($"{InstanceArgument}=", StringComparison.OrdinalIgnoreCase))
            {
                SetInstanceName(argument[(InstanceArgument.Length + 1)..], ref instanceName);
                continue;
            }

            if (argument.Equals(InstanceArgument, StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                    throw new InvalidOperationException($"Missing value after {InstanceArgument}.");

                SetInstanceName(args[++index], ref instanceName);
                continue;
            }

            hostArguments.Add(argument);
        }

        if (useLocalConfiguration && launchedByInsLauncher)
        {
            throw new InvalidOperationException(
                $"Arguments {LocalConfigurationArgument} and {InsLauncherArgument} cannot be used together.");
        }

        if (launchedByInsLauncher && instanceName is null)
        {
            throw new InvalidOperationException(
                $"An adapter started by InsLauncher requires {InstanceArgument} <name>.");
        }

        if (!launchedByInsLauncher && instanceName is not null)
        {
            throw new InvalidOperationException(
                $"Argument {InstanceArgument} can only be used with {InsLauncherArgument}.");
        }

        var mode = useLocalConfiguration
            ? AdapterStartupMode.LocalConfiguration
            : launchedByInsLauncher
                ? AdapterStartupMode.InsLauncher
                : AdapterStartupMode.Unspecified;

        return new AdapterStartupOptions(mode, instanceName, hostArguments);
    }

    public string AddInstanceConfiguration(
        ConfigurationManager configuration,
        string? commonApplicationDataPath = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (Mode != AdapterStartupMode.InsLauncher)
            throw new InvalidOperationException("Instance configuration is only available in InsLauncher mode.");

        var settingsPath = GetInstanceSettingsPath(InstanceName!, commonApplicationDataPath);

        if (!File.Exists(settingsPath))
        {
            throw new InvalidOperationException(
                $"Missing configuration for adapter instance '{InstanceName}'. Expected file: '{settingsPath}'.");
        }

        configuration.AddJsonFile(settingsPath, optional: false, reloadOnChange: false);

        // External instance settings override packaged defaults. Environment variables and
        // standard ASP.NET Core arguments remain the highest-priority providers.
        configuration.AddEnvironmentVariables();
        configuration.AddCommandLine(HostArguments.ToArray());

        return settingsPath;
    }

    public static string GetInstanceSettingsPath(
        string instanceName,
        string? commonApplicationDataPath = null)
    {
        ValidateInstanceName(instanceName);

        var rootPath = commonApplicationDataPath
            ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        if (string.IsNullOrWhiteSpace(rootPath))
            throw new InvalidOperationException("The common application data directory is unavailable.");

        return Path.Combine(
            rootPath,
            "SubiektNexoConnector",
            "instances",
            instanceName,
            SettingsFileName);
    }

    private static void SetInstanceName(string value, ref string? instanceName)
    {
        ValidateInstanceName(value);

        if (instanceName is not null && !instanceName.Equals(value, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only one adapter instance can be selected.");

        instanceName = value;
    }

    private static void ValidateInstanceName(string instanceName)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
            throw new InvalidOperationException("Adapter instance name cannot be empty.");

        if (instanceName.Length > 64)
            throw new InvalidOperationException("Adapter instance name cannot exceed 64 characters.");

        if (!instanceName.All(character =>
                char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))
        {
            throw new InvalidOperationException(
                "Adapter instance name may only contain ASCII letters, digits, '-' and '_'.");
        }
    }
}
