using System.IO;
using SubiektNexoConnector.Deployment.Windows;

namespace SubiektNexoConnector.Deployment.Lifecycle;

public static class InstallerLog
{
    private static readonly object Sync = new();
    public static string? PathName { get; private set; }
    public static void Initialize()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SubiektNexoConnector");
        ProtectedDirectory.Create(root);
        ProtectedDirectory.Create(Path.Combine(root, "instances"));
        ProtectedDirectory.Create(Path.Combine(root, "installer"));
        var directory = Path.Combine(root, "installer", "logs");
        ProtectedDirectory.Create(directory);
        PathName = Path.Combine(directory, $"setup-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.log");
        Event("startup");
    }
    public static void Event(string code, Exception? exception = null)
    {
        // Deliberately avoid raw exception messages, request bodies and configuration values.
        lock (Sync)
        {
            if (PathName is not null)
                File.AppendAllText(PathName, $"{DateTimeOffset.UtcNow:O} {code} {exception?.GetType().FullName} {(exception is null ? "" : $"0x{exception.HResult:X8}")}{Environment.NewLine}");
        }
    }
}
