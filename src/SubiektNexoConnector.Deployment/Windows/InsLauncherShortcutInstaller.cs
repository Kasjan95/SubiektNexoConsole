using System.IO;
using System.Runtime.InteropServices;
using SubiektNexoConnector.Deployment.Packaging;

namespace SubiektNexoConnector.Deployment.Windows;

public sealed class InsLauncherShortcutInstaller
{
    public InsLauncherShortcutResult Install(string instanceName, string launcherPath, string? desktopPath = null)
    {
        DeploymentNames.ValidateInstanceName(instanceName);
        var fullLauncherPath = Path.GetFullPath(launcherPath);
        if (!File.Exists(fullLauncherPath))
            throw new FileNotFoundException("Nie znaleziono InsLauncher.exe.", fullLauncherPath);

        var desktop = desktopPath ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrWhiteSpace(desktop))
            throw new InvalidOperationException("Nie można ustalić ścieżki pulpitu użytkownika.");
        Directory.CreateDirectory(desktop);

        var shortcutPath = Path.Combine(desktop, $"{DeploymentNames.GetLaunchScenarioName(instanceName)}.lnk");
        var arguments = GetArguments(instanceName);
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Usługa Windows Script Host jest niedostępna.");
        object? shell = null;
        object? shortcut = null;
        try
        {
            shell = Activator.CreateInstance(shellType)
                ?? throw new InvalidOperationException("Nie udało się uruchomić Windows Script Host.");
            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                System.Reflection.BindingFlags.InvokeMethod,
                binder: null,
                shell,
                [shortcutPath]);
            var shortcutType = shortcut!.GetType();
            shortcutType.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, [fullLauncherPath]);
            shortcutType.InvokeMember("Arguments", System.Reflection.BindingFlags.SetProperty, null, shortcut, [arguments]);
            shortcutType.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut, [Path.GetDirectoryName(fullLauncherPath)!]);
            shortcutType.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, shortcut, [$"Uruchom adapter {instanceName} przez InsLauncher"]);
            shortcutType.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
        }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut))
                Marshal.FinalReleaseComObject(shortcut);
            if (shell is not null && Marshal.IsComObject(shell))
                Marshal.FinalReleaseComObject(shell);
        }

        return new InsLauncherShortcutResult(shortcutPath, fullLauncherPath, arguments);
    }

    public static string GetDefaultLauncherPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "InsERT",
        "nexo",
        "InsLauncher.exe");

    public static void RemoveOwned(string shortcutPath, string instance, string launcher)
    {
        Lifecycle.SafePath.EnsureNoReparsePoints(shortcutPath);
        if (!File.Exists(shortcutPath)) return;
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        dynamic? shortcut = null;
        try
        {
            shortcut = shell.CreateShortcut(shortcutPath);
            if (!string.Equals((string)shortcut.TargetPath, launcher, StringComparison.OrdinalIgnoreCase)
                || shortcut.Arguments != GetArguments(instance))
                throw new InvalidOperationException("Skrót wskazuje inny program; nie usunięto go.");
            File.Delete(shortcutPath);
        }
        finally { if (shortcut is not null) Marshal.FinalReleaseComObject(shortcut); Marshal.FinalReleaseComObject(shell); }
    }
    public static string GetArguments(string instanceName)
    {
        DeploymentNames.ValidateInstanceName(instanceName);
        return $"nexo/{DeploymentNames.GetLaunchScenarioName(instanceName)}";
    }
}
