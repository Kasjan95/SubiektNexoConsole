using System.IO;
using System.IO.Compression;

namespace SubiektNexoConnector.Deployment.Packaging;

public sealed class AdapterPayloadArchiveBuilder
{
    public int Build(string publishDirectory, string outputPath)
    {
        var sourceDirectory = Path.GetFullPath(publishDirectory);
        var archivePath = Path.GetFullPath(outputPath);
        if (!archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Archiwum payloadu musi mieć rozszerzenie .zip.");
        if (IsPathInsideDirectory(archivePath, sourceDirectory))
            throw new InvalidOperationException("Archiwum payloadu nie może znajdować się wewnątrz katalogu publish.");

        var files = new PublishPayloadSelector().SelectFiles(sourceDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);
        var temporaryPath = Path.Combine(
            Path.GetDirectoryName(archivePath)!,
            $".{Path.GetFileName(archivePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false))
            {
                foreach (var file in files)
                {
                    var entryName = Path.GetRelativePath(sourceDirectory, file)
                        .Replace(Path.DirectorySeparatorChar, '/');
                    var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                    using var input = File.OpenRead(file);
                    using var output = entry.Open();
                    input.CopyTo(output);
                }
            }

            File.Move(temporaryPath, archivePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }

        return files.Count;
    }

    private static bool IsPathInsideDirectory(string path, string directory)
    {
        var relative = Path.GetRelativePath(directory, path);
        return relative != ".."
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !Path.IsPathRooted(relative);
    }
}
