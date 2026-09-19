using System.IO;
using System.Text.Json;

namespace SubiektNexoConnector.Deployment.Packaging;

public sealed class PublishPayloadSelector
{
    private static readonly HashSet<string> AllowedLibraryTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "project",
        "package",
        "runtimepack"
    };

    public IReadOnlyList<string> SelectFiles(string publishDirectory)
    {
        var root = Path.GetFullPath(publishDirectory);
        var depsPath = Path.Combine(root, "SubiektNexoConnector.Api.deps.json");
        if (!File.Exists(depsPath))
            throw new InvalidOperationException("Katalog publish nie zawiera SubiektNexoConnector.Api.deps.json.");

        var forbiddenSettings = Directory.EnumerateFiles(root, "appsettings*.json", SearchOption.TopDirectoryOnly).ToArray();
        if (forbiddenSettings.Length > 0)
            throw new InvalidOperationException("Katalog publish zawiera appsettings*.json. Przerwano pakowanie, aby nie umieścić sekretów w MPKG.");

        using var document = JsonDocument.Parse(File.ReadAllText(depsPath));
        var rootElement = document.RootElement;
        var targetName = rootElement.GetProperty("runtimeTarget").GetProperty("name").GetString()
            ?? throw new InvalidOperationException("Plik deps.json nie określa runtimeTarget.");
        var target = rootElement.GetProperty("targets").GetProperty(targetName);
        var libraries = rootElement.GetProperty("libraries");
        var allFiles = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToArray();
        var filesByName = allFiles.GroupBy(path => Path.GetFileName(path)!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddRequiredFile(selected, Path.Combine(root, MpkgBuilder.AdapterExecutableName));
        AddRequiredFile(selected, depsPath);
        AddRequiredFile(selected, Path.Combine(root, "SubiektNexoConnector.Api.runtimeconfig.json"));
        AddOptionalFiles(selected, root, "SubiektNexoConnector.Api.staticwebassets*.json");
        AddRequiredFile(selected, Path.Combine(root, "LICENSE"));
        AddRequiredFile(selected, Path.Combine(root, "THIRD-PARTY-NOTICES.txt"));

        foreach (var library in target.EnumerateObject())
        {
            if (!libraries.TryGetProperty(library.Name, out var libraryMetadata))
                continue;
            var libraryType = libraryMetadata.GetProperty("type").GetString();
            if (libraryType is null || !AllowedLibraryTypes.Contains(libraryType))
                continue;

            AddAssetGroup(library.Value, "runtime", filesByName, selected);
            AddAssetGroup(library.Value, "native", filesByName, selected);
            AddAssetGroup(library.Value, "resources", filesByName, selected);
            AddAssetGroup(library.Value, "runtimeTargets", filesByName, selected);
        }

        var wwwroot = Path.Combine(root, "wwwroot");
        if (Directory.Exists(wwwroot))
        {
            foreach (var file in Directory.EnumerateFiles(wwwroot, "*", SearchOption.AllDirectories))
                selected.Add(file);
        }

        return selected.OrderBy(path => Path.GetRelativePath(root, path), StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void AddAssetGroup(
        JsonElement library,
        string propertyName,
        IReadOnlyDictionary<string, string[]> filesByName,
        ISet<string> selected)
    {
        if (!library.TryGetProperty(propertyName, out var assets))
            return;

        foreach (var asset in assets.EnumerateObject())
        {
            if (asset.Name.EndsWith("/_._", StringComparison.Ordinal))
                continue;
            var fileName = Path.GetFileName(asset.Name);
            if (!filesByName.TryGetValue(fileName, out var matches) || matches.Length == 0)
                throw new InvalidOperationException($"Brakuje pliku runtime wskazanego przez deps.json: {asset.Name}");
            if (matches.Length > 1)
                throw new InvalidOperationException($"Niejednoznaczny plik runtime w publishu: {fileName}");
            selected.Add(matches[0]);
        }
    }

    private static void AddRequiredFile(ISet<string> selected, string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException($"Katalog publish nie zawiera wymaganego pliku: {Path.GetFileName(path)}");
        selected.Add(path);
    }

    private static void AddOptionalFiles(ISet<string> selected, string root, string pattern)
    {
        foreach (var file in Directory.EnumerateFiles(root, pattern, SearchOption.TopDirectoryOnly))
            selected.Add(file);
    }
}
