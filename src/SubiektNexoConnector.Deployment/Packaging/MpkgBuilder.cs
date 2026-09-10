using System.IO.Compression;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace SubiektNexoConnector.Deployment.Packaging;

public sealed class MpkgBuilder
{
    public const string AdapterExecutableName = "SubiektNexoConnector.Api.exe";
    public const string ManifestEntryName = "manifest.xml";

    public MpkgBuildResult Build(MpkgBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        DeploymentNames.ValidateInstanceName(request.InstanceName);

        var sourceDirectory = Path.GetFullPath(request.PublishDirectory);
        var outputPath = Path.GetFullPath(request.OutputPath);
        var packageIdentity = DeploymentNames.GetPackageIdentity(request.InstanceName, request.AdapterVersion);
        if (request.ExistingPackageIdentity is not null)
            packageIdentity = Lifecycle.AdapterUpdatePolicy.NextIdentity(request.ExistingPackageIdentity, request.AdapterVersion);
        var scenarioName = DeploymentNames.GetLaunchScenarioName(request.InstanceName);

        if (!Directory.Exists(sourceDirectory))
            throw new DirectoryNotFoundException($"Nie znaleziono katalogu publish: {sourceDirectory}");
        if (!File.Exists(Path.Combine(sourceDirectory, AdapterExecutableName)))
            throw new InvalidOperationException($"Katalog publish nie zawiera {AdapterExecutableName}.");
        if (!outputPath.EndsWith(".mpkg", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Plik wynikowy musi mieć rozszerzenie .mpkg.");
        if (string.IsNullOrWhiteSpace(request.RequiredProductVersion))
            throw new InvalidOperationException("Zakres wymaganych wersji Nexo nie może być pusty.");
        if (IsPathInsideDirectory(outputPath, sourceDirectory))
            throw new InvalidOperationException("Plik MPKG nie może znajdować się wewnątrz katalogu publish.");
        if (File.Exists(outputPath) && !request.OverwriteExisting)
            throw new IOException($"Plik wynikowy już istnieje: {outputPath}");

        var files = new PublishPayloadSelector().SelectFiles(sourceDirectory)
            .Select(path => new PackageFile(
                path,
                Path.GetRelativePath(sourceDirectory, path).Replace(Path.DirectorySeparatorChar, '/')))
            .OrderBy(file => file.EntryName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        EnsureUniqueEntryNames(files);
        var manifest = CreateManifest(files, request, packageIdentity, scenarioName);
        var outputDirectory = Path.GetDirectoryName(outputPath)!;
        Directory.CreateDirectory(outputDirectory);
        var temporaryPath = Path.Combine(outputDirectory, $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false, Encoding.UTF8))
            {
                foreach (var file in files)
                {
                    var entry = archive.CreateEntry(file.EntryName, CompressionLevel.Optimal);
                    entry.LastWriteTime = ClampZipTimestamp(File.GetLastWriteTime(file.FullPath));
                    using var input = File.OpenRead(file.FullPath);
                    using var output = entry.Open();
                    input.CopyTo(output);
                }

                var manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
                // Rebuilding the same cached payload must not change its hash just because time passed.
                manifestEntry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                using var manifestOutput = manifestEntry.Open();
                using var writer = XmlWriter.Create(manifestOutput, new XmlWriterSettings
                {
                    Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    Indent = true,
                    CloseOutput = false
                });
                manifest.Save(writer);
            }

            File.Move(temporaryPath, outputPath, request.OverwriteExisting);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }

        return new MpkgBuildResult(outputPath, packageIdentity, scenarioName, files.Length);
    }

    private static XDocument CreateManifest(
        IReadOnlyCollection<PackageFile> files,
        MpkgBuildRequest request,
        string packageIdentity,
        string scenarioName)
    {
        var contents = new XElement("Contents");
        var directoryElements = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase)
        {
            [string.Empty] = contents
        };

        foreach (var file in files)
        {
            var parts = file.EntryName.Split('/');
            var currentPath = string.Empty;
            var parent = contents;
            for (var index = 0; index < parts.Length - 1; index++)
            {
                currentPath = currentPath.Length == 0 ? parts[index] : $"{currentPath}/{parts[index]}";
                if (!directoryElements.TryGetValue(currentPath, out var directory))
                {
                    directory = new XElement("Directory", new XAttribute("Name", parts[index]));
                    parent.Add(directory);
                    directoryElements[currentPath] = directory;
                }
                parent = directory;
            }

            var info = new FileInfo(file.FullPath);
            parent.Add(new XElement("File",
                new XAttribute("Name", parts[^1]),
                new XAttribute("Size", info.Length),
                new XAttribute("Time", info.LastWriteTimeUtc.ToFileTimeUtc())));
        }

        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("Package",
                new XAttribute("Identity", packageIdentity),
                new XAttribute("DisplayName", scenarioName),
                new XAttribute("VersionDisplayName", DeploymentNames.NormalizeVersion(request.AdapterVersion)),
                new XAttribute("RequiredProductVersion", request.RequiredProductVersion),
                contents,
                new XElement("LaunchScenarios",
                    new XElement("LaunchScenario",
                        new XAttribute("Name", scenarioName),
                        new XAttribute("CanUpdate", false),
                        new XElement("LaunchAction",
                            new XAttribute("ExePath", AdapterExecutableName),
                            new XAttribute("CommandLineArguments", $"/UruchomionePrzezInsLauncher --instance {request.InstanceName}"),
                            new XAttribute("RedirectedInput", "!LauncherMessageFormatB!"),
                            new XAttribute("RedirectedInputEncoding", "utf-8")))),
                new XElement("DeployActions"),
                new XElement("Dependencies")));
    }

    private static void EnsureUniqueEntryNames(IEnumerable<PackageFile> files)
    {
        var duplicate = files.GroupBy(file => file.EntryName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"Katalog publish zawiera niejednoznaczną ścieżkę: {duplicate.Key}");
    }

    private static bool IsPathInsideDirectory(string path, string directory)
    {
        var relative = Path.GetRelativePath(directory, path);
        return relative != ".."
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !Path.IsPathRooted(relative);
    }

    private static DateTimeOffset ClampZipTimestamp(DateTime timestamp)
    {
        var value = new DateTimeOffset(timestamp);
        var minimum = new DateTimeOffset(1980, 1, 1, 0, 0, 0, value.Offset);
        var maximum = new DateTimeOffset(2107, 12, 31, 23, 59, 58, value.Offset);
        return value < minimum ? minimum : value > maximum ? maximum : value;
    }

    private sealed record PackageFile(string FullPath, string EntryName);
}
