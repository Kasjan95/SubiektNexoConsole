using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using SubiektNexoConnector.Deployment.Packaging;
using SubiektNexoConnector.Deployment.Lifecycle;

namespace SubiektNexoConnector.Deployment.Configuration;

public sealed class InstanceSettingsWriter
{
    public const string IdempotencyDatabaseFileName = "idempotency.db";
    public const string ReadModelDatabaseFileName = "read-model.db";

    public JsonObject? ReadForEdit(string instanceName, string? commonApplicationDataPath = null)
    {
        var path = GetSettingsPath(instanceName, commonApplicationDataPath);
        SafePath.EnsureNoReparsePoints(path);
        try
        {
            return JsonNode.Parse(File.ReadAllText(path)) as JsonObject
                ?? throw new InvalidDataException("Konfiguracja nie jest obiektem JSON. Przywróć jej kopię zapasową.");
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    public ExistingInstanceSettings? ReadExisting(
        string instanceName,
        string? commonApplicationDataPath = null)
    {
        var settingsPath = GetSettingsPath(instanceName, commonApplicationDataPath);
        if (!File.Exists(settingsPath))
            return null;

        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
        var root = document.RootElement;
        var apiKey = root.GetProperty("Auth").GetProperty("ApiKey").GetString();
        var urls = root.GetProperty("Urls").GetString();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException($"Istniejąca konfiguracja {settingsPath} nie zawiera API key.");
        if (!Uri.TryCreate(urls, UriKind.Absolute, out var uri) || uri.Port is < 1 or > 65535)
            throw new InvalidOperationException($"Istniejąca konfiguracja {settingsPath} nie zawiera poprawnego portu.");

        return new ExistingInstanceSettings(apiKey, uri.Port);
    }

    public InstanceSettingsResult Write(InstanceSettingsRequest request, string? commonApplicationDataPath = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        DeploymentNames.ValidateInstanceName(request.InstanceName);
        if (request.Port is < 1 or > 65535)
            throw new InvalidOperationException("Port musi mieścić się w zakresie 1-65535.");
        if (string.IsNullOrWhiteSpace(request.ApiKey))
            throw new InvalidOperationException("API key nie może być pusty.");
        if (string.IsNullOrWhiteSpace(request.NexoUser))
            throw new InvalidOperationException("Operator Nexo nie może być pusty.");
        if (string.IsNullOrWhiteSpace(request.NexoPassword))
            throw new InvalidOperationException("Hasło operatora Nexo nie może być puste.");
        if (!string.IsNullOrWhiteSpace(request.SeqUrl)
            && (!Uri.TryCreate(request.SeqUrl, UriKind.Absolute, out var seqUri)
                || (seqUri.Scheme != Uri.UriSchemeHttp && seqUri.Scheme != Uri.UriSchemeHttps)))
        {
            throw new InvalidOperationException("Adres Seq musi być pełnym adresem HTTP/HTTPS albo pozostać pusty.");
        }

        var settingsPath = GetSettingsPath(request.InstanceName, commonApplicationDataPath);
        var idempotencyDatabasePath = GetIdempotencyDatabasePath(request.InstanceName, commonApplicationDataPath);
        var readModelDatabasePath = GetReadModelDatabasePath(request.InstanceName, commonApplicationDataPath);
        SafePath.EnsureNoReparsePoints(settingsPath);
        if (File.Exists(settingsPath) && !request.OverwriteExisting)
            throw new IOException($"Konfiguracja instancji już istnieje: {settingsPath}");

        var writeTo = new JsonArray
        {
            new JsonObject { ["Name"] = "Console" },
            new JsonObject
            {
                ["Name"] = "File",
                ["Args"] = new JsonObject
                {
                    ["path"] = Path.Combine(Path.GetDirectoryName(settingsPath)!, "logs", "adapter-.log"),
                    ["rollingInterval"] = "Day",
                    ["retainedFileCountLimit"] = 14,
                    ["shared"] = true
                }
            }
        };
        if (!string.IsNullOrWhiteSpace(request.SeqUrl))
        {
            writeTo.Add(new JsonObject
            {
                ["Name"] = "Seq",
                ["Args"] = new JsonObject { ["serverUrl"] = request.SeqUrl.Trim() }
            });
        }

        var settings = new JsonObject
        {
            ["Urls"] = $"http://0.0.0.0:{request.Port}",
            ["Auth"] = new JsonObject
            {
                ["Mode"] = "ApiKey",
                ["HeaderName"] = "X-Api-Key",
                ["ApiKey"] = request.ApiKey
            },
            ["Observability"] = new JsonObject
            {
                ["Service"] = "SubiektNexoConnector.Api",
                ["AdapterInstance"] = request.InstanceName,
                ["NexoCompany"] = request.NexoCompany.Trim()
            },
            ["Api"] = new JsonObject
            {
                ["Pagination"] = new JsonObject
                {
                    ["DefaultPageSize"] = 100,
                    ["MaxPageSize"] = 1000
                }
            },
            ["Idempotency"] = new JsonObject
            {
                ["DatabasePath"] = idempotencyDatabasePath
            },
            ["Materialization"] = new JsonObject
            {
                ["DatabasePath"] = readModelDatabasePath
            },
            ["Nexo"] = new JsonObject
            {
                ["SystemLogin"] = new JsonObject
                {
                    ["NexoUser"] = request.NexoUser.Trim(),
                    ["NexoPassword"] = request.NexoPassword
                },
                ["SferaExecution"] = new JsonObject
                {
                    ["QueueTimeoutSeconds"] = 30,
                    ["RetryAfterSeconds"] = 5
                }
            },
            ["Logging"] = new JsonObject
            {
                ["LogLevel"] = new JsonObject
                {
                    ["Default"] = "Information",
                    ["Microsoft.AspNetCore"] = "Warning"
                }
            },
            ["Serilog"] = new JsonObject
            {
                ["Using"] = new JsonArray("Serilog.Sinks.Console", "Serilog.Sinks.File", "Serilog.Sinks.Seq"),
                ["MinimumLevel"] = new JsonObject
                {
                    ["Default"] = "Information",
                    ["Override"] = new JsonObject
                    {
                        ["Microsoft"] = "Warning",
                        ["Microsoft.AspNetCore"] = "Warning",
                        ["Microsoft.Hosting.Lifetime"] = "Information",
                        ["System"] = "Warning"
                    }
                },
                ["Enrich"] = new JsonArray("FromLogContext"),
                ["Properties"] = new JsonObject { ["Application"] = "SubiektNexoConnector.Api" },
                ["WriteTo"] = writeTo
            },
            ["AllowedHosts"] = "*"
        };

        if (File.Exists(settingsPath) && request.OverwriteExisting)
        {
            var existing = JsonNode.Parse(File.ReadAllText(settingsPath)) as JsonObject
                ?? throw new InvalidDataException("Istniejąca konfiguracja jest uszkodzona. Zachowano oryginał.");
            DeploymentStore.AtomicWrite(settingsPath + ".backup", existing.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            // Preserve every unrelated / future option, changing only the fields owned by the form.
            existing["Urls"] = settings["Urls"]!.DeepClone();
            Set(existing, "Auth", "ApiKey", request.ApiKey);
            Set(existing, "Auth", "Mode", "ApiKey");
            Set(existing, "Auth", "HeaderName", "X-Api-Key");
            Set(existing, "Observability", "NexoCompany", request.NexoCompany.Trim());
            Set(existing, "Observability", "AdapterInstance", request.InstanceName);
            Set(existing, "Idempotency", "DatabasePath", idempotencyDatabasePath);
            Set(existing, "Materialization", "DatabasePath", readModelDatabasePath);
            var nexo = existing["Nexo"] as JsonObject ?? new JsonObject();
            if (existing["Nexo"] is null) existing["Nexo"] = nexo;
            Set(nexo, "SystemLogin", "NexoUser", request.NexoUser.Trim());
            Set(nexo, "SystemLogin", "NexoPassword", request.NexoPassword);
            var serilog = existing["Serilog"] as JsonObject ?? new JsonObject();
            if (existing["Serilog"] is null) existing["Serilog"] = serilog;
            var sinks = serilog["WriteTo"] as JsonArray ?? new JsonArray();
            if (serilog["WriteTo"] is null) serilog["WriteTo"] = sinks;
            for (var index = sinks.Count - 1; index >= 0; index--)
                if (string.Equals(sinks[index]?["Name"]?.GetValue<string>(), "Seq", StringComparison.OrdinalIgnoreCase)) sinks.RemoveAt(index);
            if (!string.IsNullOrWhiteSpace(request.SeqUrl)) sinks.Add(new JsonObject { ["Name"] = "Seq", ["Args"] = new JsonObject { ["serverUrl"] = request.SeqUrl.Trim() } });
            settings = existing;
        }

        var directory = Path.GetDirectoryName(settingsPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(settingsPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryPath, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, settingsPath, request.OverwriteExisting);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }

        return new InstanceSettingsResult(settingsPath);
    }

    public void EnsurePersistenceConfiguration(
        string instanceName,
        string? commonApplicationDataPath = null)
    {
        var settingsPath = GetSettingsPath(instanceName, commonApplicationDataPath);
        SafePath.EnsureNoReparsePoints(settingsPath);
        var settings = JsonNode.Parse(File.ReadAllText(settingsPath)) as JsonObject
            ?? throw new InvalidDataException("Konfiguracja nie jest obiektem JSON. Zachowano oryginał.");
        Set(settings, "Idempotency", "DatabasePath",
            GetIdempotencyDatabasePath(instanceName, commonApplicationDataPath));
        Set(settings, "Materialization", "DatabasePath",
            GetReadModelDatabasePath(instanceName, commonApplicationDataPath));
        DeploymentStore.AtomicWrite(
            settingsPath,
            settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void Set(JsonObject root, string section, string key, string value)
    {
        if (root[section] is null) root[section] = new JsonObject();
        root[section]![key] = value;
    }

    public static string GetSettingsPath(string instanceName, string? commonApplicationDataPath = null)
    {
        DeploymentNames.ValidateInstanceName(instanceName);
        var root = commonApplicationDataPath
            ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("Nie można ustalić katalogu ProgramData.");

        return Path.Combine(root, "SubiektNexoConnector", "instances", instanceName, "settings.json");
    }

    public static string GetIdempotencyDatabasePath(
        string instanceName,
        string? commonApplicationDataPath = null)
    {
        var instanceDirectory = Path.GetDirectoryName(GetSettingsPath(instanceName, commonApplicationDataPath))!;
        return Path.Combine(instanceDirectory, "data", IdempotencyDatabaseFileName);
    }

    public static string GetReadModelDatabasePath(
        string instanceName,
        string? commonApplicationDataPath = null)
    {
        var instanceDirectory = Path.GetDirectoryName(GetSettingsPath(instanceName, commonApplicationDataPath))!;
        return Path.Combine(instanceDirectory, "data", ReadModelDatabaseFileName);
    }
}
