using System.IO;
using System.Security.Cryptography;
using Microsoft.Win32;
using SubiektNexoConnector.Deployment.Lifecycle;
using SubiektNexoConnector.Deployment.Packaging;

namespace SubiektNexoConnector.Deployment.Windows;

public static class MaintenanceRegistration
{
    private const string RegistryRoot = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\";
    public static string CacheInstaller()
    {
        var source = Environment.ProcessPath ?? throw new InvalidOperationException("Nie znaleziono instalatora.");
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source)));
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SubiektNexoConnector", "installer");
        ProtectedDirectory.Create(root);
        var directory = Path.Combine(root, hash);
        ProtectedDirectory.Create(directory);
        var destination = Path.Combine(directory, "SubiektNexoConnector.Setup.exe");
        if (!source.Equals(destination, StringComparison.OrdinalIgnoreCase)) File.Copy(source, destination, true);
        if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(destination))) != hash)
            throw new InvalidDataException("Nieprawidłowa kopia instalatora.");
        return destination;
    }
    public static void Register(DeploymentState state)
    {
        var executable = CacheInstaller();
        using var key = Registry.LocalMachine.CreateSubKey(RegistryRoot + DeploymentNames.GetLaunchScenarioName(state.Instance));
        key.SetValue("DisplayName", $"SubiektNexoConnector — {state.Instance}");
        key.SetValue("DisplayVersion", state.AdapterVersion);
        key.SetValue("Publisher", "SubiektNexoConnector");
        key.SetValue("UninstallString", $"\"{executable}\" --manage --instance {state.Instance}");
        key.SetValue("ModifyPath", $"\"{executable}\" --manage --instance {state.Instance}");
        key.SetValue("DisplayIcon", executable);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }
    public static void Remove(string instance)
    {
        Registry.LocalMachine.DeleteSubKey(RegistryRoot + DeploymentNames.GetLaunchScenarioName(instance), false);
    }
}
