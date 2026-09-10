using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using SubiektNexoConnector.Deployment.Lifecycle;

namespace SubiektNexoConnector.Deployment.Windows;

public static class ProtectedDirectory
{
    public static void Create(string path, string? readerSid = null, bool modify = false)
    {
        SafePath.EnsureNoReparsePoints(path);
        var acl = new DirectorySecurity();
        acl.SetAccessRuleProtection(true, false);
        var inheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        foreach (var sid in new[] { "S-1-5-18", "S-1-5-32-544" })
            acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid), FileSystemRights.FullControl,
                inheritance, PropagationFlags.None, AccessControlType.Allow));
        if (readerSid is not null && readerSid is not "S-1-5-18" and not "S-1-5-32-544")
            acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(readerSid),
                modify ? FileSystemRights.Modify : FileSystemRights.ReadAndExecute,
                inheritance, PropagationFlags.None, AccessControlType.Allow));
        var info = new DirectoryInfo(path);
        if (info.Exists) info.SetAccessControl(acl);
        else info.Create(acl);
    }
    public static void ResetFile(string path, string? readerSid = null)
    {
        SafePath.EnsureNoReparsePoints(path);
        if (!File.Exists(path)) return;
        var acl = new FileSecurity(); acl.SetAccessRuleProtection(true, false);
        foreach (var sid in new[] { "S-1-5-18", "S-1-5-32-544" })
            acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid), FileSystemRights.FullControl, AccessControlType.Allow));
        if (readerSid is not null) acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(readerSid), FileSystemRights.ReadAndExecute, AccessControlType.Allow));
        new FileInfo(path).SetAccessControl(acl);
    }
}
