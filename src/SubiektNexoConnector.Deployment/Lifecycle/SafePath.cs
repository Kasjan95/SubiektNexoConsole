using System.IO;

namespace SubiektNexoConnector.Deployment.Lifecycle;

public static class SafePath
{
    public static string Child(string root, string path)
    {
        var full = Path.GetFullPath(path);
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Ścieżka wykracza poza katalog zarządzany przez instalator.");
        EnsureNoReparsePoints(full);
        return full;
    }
    public static void EnsureNoReparsePoints(string path)
    {
        for (var current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current))
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Katalog instalacji nie może zawierać dowiązań ani punktów ponownej analizy.");
        }
    }
    public static void DeleteTree(string root, string directory)
    {
        var target = Child(root, directory);
        if (!Directory.Exists(target)) return;
        // Inspect before deleting anything; never traverse a reparse point.
        InspectTree(target);
        Directory.Delete(target, true);
    }
    private static void InspectTree(string directory)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
        {
            EnsureNoReparsePoints(path);
            if (Directory.Exists(path)) InspectTree(path);
        }
    }
}
