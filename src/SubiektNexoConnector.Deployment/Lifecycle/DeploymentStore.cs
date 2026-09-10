using System.IO;
using System.Text.Json;
using SubiektNexoConnector.Deployment.Packaging;

namespace SubiektNexoConnector.Deployment.Lifecycle;

public sealed class DeploymentStore(string directory, string? expectedInstance = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    public void Save(DeploymentState state)
    {
        DeploymentNames.ValidateInstanceName(state.Instance);
        if (expectedInstance is not null && !state.Instance.Equals(expectedInstance, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Stan instalacji dotyczy innej instancji.");
        state.UpdatedAt = DateTimeOffset.UtcNow;
        AtomicWrite(Path.Combine(DirectoryPath, "deployment.json"), JsonSerializer.Serialize(state, JsonOptions));
    }
    public DeploymentState? Load()
    {
        var path = Path.Combine(DirectoryPath, "deployment.json");
        if (!File.Exists(path)) return null;
        SafePath.EnsureNoReparsePoints(path);
        var state = JsonSerializer.Deserialize<DeploymentState>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Nie można odczytać stanu instalacji.");
        if (state.SchemaVersion != 1) throw new InvalidDataException("Nowszy lub nieznany format instalacji. Użyj odpowiedniego instalatora.");
        DeploymentNames.ValidateInstanceName(state.Instance);
        if (expectedInstance is not null && !state.Instance.Equals(expectedInstance, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Stan instalacji dotyczy innej instancji.");
        return state;
    }
    public static void AtomicWrite(string path, string text)
    {
        SafePath.EnsureNoReparsePoints(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using var writer = new StreamWriter(stream, leaveOpen: true);
                writer.Write(text);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
