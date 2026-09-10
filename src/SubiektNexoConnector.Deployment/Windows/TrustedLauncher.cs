using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using SubiektNexoConnector.Deployment.Lifecycle;

namespace SubiektNexoConnector.Deployment.Windows;

public static class TrustedLauncher
{
    public static void Validate(string launcher)
    {
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        SafePath.Child(programs, launcher);
        var allowedWriters = new HashSet<string>(StringComparer.Ordinal)
        {
            "S-1-5-18", "S-1-5-32-544",
            "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464" // TrustedInstaller
        };
        for (var path = Path.GetDirectoryName(launcher); path is not null; path = Path.GetDirectoryName(path))
        {
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException("Nie znaleziono katalogu InsLaunchera.");
            Check(new DirectoryInfo(path).GetAccessControl(), allowedWriters);
            if (path.Equals(programs, StringComparison.OrdinalIgnoreCase)) break;
        }
        Check(new FileInfo(launcher).GetAccessControl(), allowedWriters);
    }
    private static void Check(FileSystemSecurity acl, HashSet<string> allowedWriters)
    {
        const FileSystemRights write = FileSystemRights.Write | FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles
            | FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;
        foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
        {
            if (rule.AccessControlType != AccessControlType.Allow || (rule.PropagationFlags & PropagationFlags.InheritOnly) != 0) continue;
            if ((rule.FileSystemRights & write) != 0 && !allowedWriters.Contains(rule.IdentityReference.Value))
                throw new InvalidOperationException("Katalog lub plik InsLaunchera ma uprawnienia zapisu dla konta spoza administratorów/systemu. Popraw uprawnienia instalacji Nexo.");
        }
    }
}
