using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using SubiektNexoConnector.Deployment.Configuration;
using SubiektNexoConnector.Deployment.Packaging;
using SubiektNexoConnector.Deployment.Windows;

namespace SubiektNexoConnector.Deployment.Lifecycle;

public static class PilotImporter
{
    public static (string Instance, string Identity, string Version) Inspect(string packagePath)
    {
        SafePath.EnsureNoReparsePoints(packagePath);
        using var archive = ZipFile.OpenRead(packagePath);
        using var stream = (archive.GetEntry("manifest.xml") ?? throw new InvalidDataException("Brak manifestu MPKG.")).Open();
        var xml = XDocument.Load(stream);
        var root = xml.Root ?? throw new InvalidDataException("Nieprawidłowy manifest.");
        var scenario = root.Element("LaunchScenarios")?.Elements("LaunchScenario").Single()
            ?? throw new InvalidDataException("Brak jednoznacznego scenariusza.");
        var name = scenario.Attribute("Name")?.Value ?? "";
        const string prefix = "SubiektNexoConnector.";
        if (!name.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidDataException("To nie jest pakiet adaptera.");
        var instance = name[prefix.Length..]; DeploymentNames.ValidateInstanceName(instance);
        var action = scenario.Element("LaunchAction")!;
        if (action.Attribute("ExePath")?.Value != MpkgBuilder.AdapterExecutableName
            || action.Attribute("CommandLineArguments")?.Value != $"/UruchomionePrzezInsLauncher --instance {instance}")
            throw new InvalidDataException("Scenariusz nie wskazuje jednoznacznie instancji adaptera.");
        var identity = root.Attribute("Identity")?.Value ?? throw new InvalidDataException("Brak tożsamości pakietu.");
        var split = identity.LastIndexOf('-');
        if (split < 0 || !System.Version.TryParse(identity[(split + 1)..], out var version)) throw new InvalidDataException("Brak wersji pakietu.");
        return (instance, identity, DeploymentNames.NormalizeVersion(version));
    }
    public static DeploymentState Import(string package, string server, string launcher, WindowsCredentials account, string database)
    {
        var manifest = Inspect(package);
        if (DeploymentService.Store(manifest.Instance).Load() is not null) throw new InvalidOperationException("Instancja jest już zarządzana.");
        if (!AdapterScheduledTaskInstaller.Matches(manifest.Instance, launcher, account.Sid, requireEnabled: false, requirePassword: false))
            throw new InvalidOperationException("Konto lub scenariusz nie zgadza się z istniejącym zadaniem. Popraw powiązanie przed przejęciem.");
        AdapterScheduledTaskInstaller.ValidateCredentials(manifest.Instance, launcher, account);
        var settings = new InstanceSettingsWriter().ReadExisting(manifest.Instance) ?? throw new InvalidDataException("Brak konfiguracji pilota.");
        new ProgramDataAclInstaller().Install(manifest.Instance, account.Account);
        var store = DeploymentService.Store(manifest.Instance);
        var destination = Path.Combine(store.DirectoryPath, Path.GetFileName(package));
        SafePath.Child(store.DirectoryPath, destination);
        if (!destination.Equals(Path.GetFullPath(package), StringComparison.OrdinalIgnoreCase)) File.Copy(package, destination, false);
        var state = new DeploymentState
        {
            Instance = manifest.Instance,
            PackageIdentity = manifest.Identity,
            AdapterVersion = manifest.Version,
            PackagePath = destination,
            PackageSha256 = DeploymentService.Hash(destination),
            Port = settings.Port,
            Account = account.Account,
            AccountSid = account.Sid,
            Server = server,
            Database = database,
            LauncherPath = launcher,
            PackageInstalled = true,
            Status = OperationStatus.NeedsRepair
        };
        store.Save(state);
        new FirewallRuleInstaller().AdoptPilotRule(state.Instance, state.Port);
        account.Use(password => new AdapterScheduledTaskInstaller().Install(state.Instance, launcher, account.Account, password));
        new ApiKeyFileWriter().Write(store.DirectoryPath, state.Instance, settings.ApiKey, true);
        return state;
    }
}
