using System.IO;
using System.Security.Principal;
using SubiektNexoConnector.Deployment.Configuration;
namespace SubiektNexoConnector.Deployment.Windows;

public sealed class ProgramDataAclInstaller
{
    public string Install(string instanceName, string windowsAccount)
    {
        var directory = Path.GetDirectoryName(InstanceSettingsWriter.GetSettingsPath(instanceName))!;
        var sid = ((SecurityIdentifier)new NTAccount(windowsAccount).Translate(typeof(SecurityIdentifier))).Value;
        ProtectedDirectory.Create(directory, sid);
        ProtectedDirectory.ResetFile(Path.Combine(directory, "settings.json"), sid);
        ProtectedDirectory.ResetFile(Path.Combine(directory, "settings.json.backup"));
        ProtectedDirectory.Create(Path.Combine(directory, "logs"), sid, modify: true);
        // SQLite needs directory-level access to create and remove its -wal and -shm files.
        ProtectedDirectory.Create(Path.Combine(directory, "data"), sid, modify: true);
        ProtectedDirectory.Create(Path.Combine(directory, "maintenance"));
        return directory;
    }
}
