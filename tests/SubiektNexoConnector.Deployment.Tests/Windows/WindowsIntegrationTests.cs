using System.Security.AccessControl;
using System.Security.Principal;
using SubiektNexoConnector.Deployment.Windows;

namespace SubiektNexoConnector.Deployment.Tests.Windows;

public sealed class WindowsIntegrationFactAttribute : FactAttribute
{
    public WindowsIntegrationFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("CONNECTOR_WINDOWS_INTEGRATION_TESTS") != "1")
            Skip = "Opt-in Windows integration test; set CONNECTOR_WINDOWS_INTEGRATION_TESTS=1 on an elevated disposable test machine.";
        else if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            Skip = "An elevated Windows token is required.";
    }
}

public sealed class WindowsIntegrationTests
{
    [WindowsIntegrationFact]
    public void SchedulerRegistersDisabledTaskWithSeparatedArguments_AndRemovesIt()
    {
        var taskName = "Integration - " + Guid.NewGuid().ToString("N");
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        dynamic? service = null, folder = null, task = null, definition = null, actions = null, action = null;
        try
        {
            AdapterScheduledTaskInstaller.RegisterBootTask(taskName, executable, "/c exit 0", Path.GetDirectoryName(executable)!,
                "SYSTEM", null, 5, "PT30S", "Isolated connector test; never started", enabled: false);
            service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!)!;
            service.Connect(); folder = service.GetFolder(@"\SubiektNexoConnector"); task = folder.GetTask(taskName);
            Assert.False((bool)task.Enabled);
            definition = task.Definition; actions = definition.Actions; action = actions.Item(1);
            Assert.Equal(executable, (string)action.Path);
            Assert.Equal("/c exit 0", (string)action.Arguments);
        }
        finally
        {
            foreach (object? com in new object?[] { action, actions, definition, task, folder, service })
                if (com is not null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(com);
            AdapterScheduledTaskInstaller.DeleteTask(taskName);
        }
    }
    [WindowsIntegrationFact]
    public void FirewallCreatesVerifiesAndRemovesOnlyItsUniqueRule()
    {
        var instance = "integration-" + Guid.NewGuid().ToString("N");
        var firewall = new FirewallRuleInstaller();
        try
        {
            firewall.Install(instance, 15997, "127.0.0.1", "private");
            Assert.True(firewall.Matches(instance, 15997, "127.0.0.1/32", "private"));
            firewall.Install(instance, 15998, "127.0.0.1", "private");
            Assert.True(firewall.Matches(instance, 15998, "127.0.0.1", "private"));
        }
        finally { firewall.Remove(instance); }
        Assert.False(firewall.Matches(instance, 15998, "127.0.0.1", "private"));
    }
    [WindowsIntegrationFact]
    public void ProtectedDirectoryRemovesInheritedAndExplicitBroadAccess()
    {
        var root = Path.Combine(Path.GetTempPath(), "connector-acl-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var before = new DirectoryInfo(root).GetAccessControl();
            before.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier("S-1-1-0"), FileSystemRights.FullControl, AccessControlType.Allow));
            new DirectoryInfo(root).SetAccessControl(before);
            ProtectedDirectory.Create(root);
            var after = new DirectoryInfo(root).GetAccessControl();
            Assert.True(after.AreAccessRulesProtected);
            var rules = after.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToArray();
            Assert.All(rules, rule => Assert.Contains(rule.IdentityReference.Value, new[] { "S-1-5-18", "S-1-5-32-544" }));
        }
        finally { Directory.Delete(root, true); }
    }

    [WindowsIntegrationFact]
    public void ProtectedDirectoryGrantsInheritedModifyAccessForSqliteFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "connector-sqlite-acl-test-" + Guid.NewGuid().ToString("N"));
        var currentSid = WindowsIdentity.GetCurrent().User!;
        try
        {
            ProtectedDirectory.Create(root, currentSid.Value, modify: true);
            var rules = new DirectoryInfo(root).GetAccessControl()
                .GetAccessRules(true, true, typeof(SecurityIdentifier))
                .Cast<FileSystemAccessRule>();
            var rule = Assert.Single(rules,
                candidate => candidate.IdentityReference.Value == currentSid.Value);

            Assert.Equal(FileSystemRights.Modify, rule.FileSystemRights & FileSystemRights.Modify);
            Assert.Equal(
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                rule.InheritanceFlags);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
    [WindowsIntegrationFact]
    public void ShortcutCanBeRemovedOnlyWhenItsTargetMatches()
    {
        var root = Path.Combine(Path.GetTempPath(), "connector-shortcut-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var executable = Path.Combine(root, "InsLauncher.exe"); File.WriteAllText(executable, "not executed");
            var result = new InsLauncherShortcutInstaller().Install("integration", executable, root);
            Assert.Throws<InvalidOperationException>(() => InsLauncherShortcutInstaller.RemoveOwned(result.ShortcutPath, "different", executable));
            Assert.True(File.Exists(result.ShortcutPath));
            InsLauncherShortcutInstaller.RemoveOwned(result.ShortcutPath, "integration", executable);
            Assert.False(File.Exists(result.ShortcutPath));
        }
        finally { Directory.Delete(root, true); }
    }
}
