using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace SubiektNexoConnector.Deployment.Packaging;

public sealed class EmbeddedAdapterPayload
{
    public const string ResourceName = "SubiektNexoConnector.Deployment.Resources.adapter-payload.zip";

    public bool IsAvailable
    {
        get
        {
            using var stream = OpenResource();
            return stream is not null;
        }
    }

    public string ExtractToTemporaryDirectory()
    {
        using var stream = OpenResource()
            ?? throw new InvalidOperationException("Installer nie zawiera payloadu adaptera.");
        var targetDirectory = Path.Combine(
            Path.GetTempPath(),
            "SubiektNexoConnector.Deployment",
            Guid.NewGuid().ToString("N"));
        Windows.ProtectedDirectory.Create(targetDirectory);

        try
        {
            ExtractArchive(stream, targetDirectory);
            return targetDirectory;
        }
        catch
        {
            Directory.Delete(targetDirectory, recursive: true);
            throw;
        }
    }

    public static void ExtractArchive(Stream stream, string targetDirectory)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var root = Path.GetFullPath(targetDirectory);
        Directory.CreateDirectory(root);
        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        foreach (var entry in archive.Entries)
        {
            var destination = Path.GetFullPath(Path.Combine(root, entry.FullName));
            if (!destination.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Niebezpieczna ścieżka w payloadzie adaptera: {entry.FullName}");

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: false);
        }
    }

    private static Stream? OpenResource() =>
        typeof(EmbeddedAdapterPayload).Assembly.GetManifestResourceStream(ResourceName);
}
