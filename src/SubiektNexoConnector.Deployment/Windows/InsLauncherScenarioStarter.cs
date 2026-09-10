using System.Diagnostics;
using System.IO;

namespace SubiektNexoConnector.Deployment.Windows;

public sealed class InsLauncherScenarioStarter
{
    public int Start(string instanceName, string launcherPath)
    {
        var fullLauncherPath = Path.GetFullPath(launcherPath);
        if (!File.Exists(fullLauncherPath))
            throw new FileNotFoundException("Nie znaleziono InsLauncher.exe.", fullLauncherPath);

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = fullLauncherPath,
            Arguments = InsLauncherShortcutInstaller.GetArguments(instanceName),
            WorkingDirectory = Path.GetDirectoryName(fullLauncherPath)!,
            UseShellExecute = true
        }) ?? throw new InvalidOperationException("Nie udało się uruchomić scenariusza przez InsLauncher.");

        return process.Id;
    }
}
