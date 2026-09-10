using System.IO;
using System.Text;
using SubiektNexoConnector.Deployment.Packaging;

namespace SubiektNexoConnector.Deployment.Configuration;

public sealed class ApiKeyFileWriter
{
    public ApiKeyFileResult Write(
        string outputDirectory,
        string instanceName,
        string apiKey,
        bool overwriteExisting = false)
    {
        DeploymentNames.ValidateInstanceName(instanceName);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("API key nie może być pusty.");

        var directory = Path.GetFullPath(outputDirectory);
        var outputPath = Path.Combine(directory, DeploymentNames.GetApiKeyFileName(instanceName));
        if (File.Exists(outputPath) && !overwriteExisting)
            throw new IOException($"Plik API key już istnieje: {outputPath}");

        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");
        var contents = $"Instance: {instanceName}{Environment.NewLine}" +
                       $"Header: X-Api-Key{Environment.NewLine}" +
                       $"ApiKey: {apiKey}{Environment.NewLine}";
        try
        {
            File.WriteAllText(temporaryPath, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, outputPath, overwriteExisting);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }

        return new ApiKeyFileResult(outputPath);
    }
}
