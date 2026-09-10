using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using SubiektNexoConnector.Deployment.Configuration;
using SubiektNexoConnector.Deployment.Packaging;
using SubiektNexoConnector.Deployment.Windows;

namespace SubiektNexoConnector.Deployment.Testing;

public sealed class PostRebootTestCoordinator
{
    public const string RunArgument = "--post-reboot-test";
    public const string ShowArgument = "--show-post-reboot-result";
    public const string InstanceArgument = "--instance";

    public PostRebootTestPreparation Prepare(string instanceName, string accountName)
    {
        DeploymentNames.ValidateInstanceName(instanceName);
        var sourceExecutable = Environment.ProcessPath
            ?? throw new InvalidOperationException("Nie można ustalić ścieżki instalatora.");
        var toolsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "SubiektNexoConnector",
            "installer");
        ProtectedDirectory.Create(toolsDirectory);
        var cachedExecutable = MaintenanceRegistration.CacheInstaller();

        var reportPath = GetReportPath(instanceName);
        Lifecycle.DeploymentStore.AtomicWrite(reportPath + ".request.json", JsonSerializer.Serialize(new
        {
            operationId = Guid.NewGuid(),
            preparedAt = DateTimeOffset.UtcNow,
            bootEstimate = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64)
        }));
        if (File.Exists(reportPath))
            File.Delete(reportPath);
        var shownMarkerPath = GetShownMarkerPath(reportPath);
        if (File.Exists(shownMarkerPath))
            File.Delete(shownMarkerPath);

        AdapterScheduledTaskInstaller.RegisterBootTask(
            GetVerifierTaskName(instanceName),
            cachedExecutable,
            $"{RunArgument} {InstanceArgument} {instanceName}",
            toolsDirectory,
            "SYSTEM",
            accountPassword: null,
            logonType: 5,
            delay: "PT45S",
            description: $"Jednorazowo sprawdza adapter {instanceName} po ponownym uruchomieniu komputera.");

        AdapterScheduledTaskInstaller.RegisterLogonTask(
            GetViewerTaskName(instanceName),
            cachedExecutable,
            $"{ShowArgument} {InstanceArgument} {instanceName}",
            accountName,
            $"Pokazuje wynik jednorazowego testu adaptera {instanceName} po restarcie.");

        return new PostRebootTestPreparation(cachedExecutable, reportPath);
    }

    public async Task RunHeadlessAsync(string instanceName)
    {
        var reportPath = GetReportPath(instanceName);
        if (File.Exists(reportPath))
        {
            AdapterScheduledTaskInstaller.DeleteTask(GetVerifierTaskName(instanceName));
            return;
        }
        try
        {
            using var intent = JsonDocument.Parse(File.ReadAllText(reportPath + ".request.json"));
            var previousBoot = intent.RootElement.GetProperty("bootEstimate").GetDateTimeOffset();
            var thisBoot = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64);
            if (Math.Abs((thisBoot - previousBoot).TotalSeconds) < 10)
                throw new InvalidOperationException("Nie wykryto nowego uruchomienia systemu.");
            var (port, apiKey) = ReadConnectionSettings(instanceName);
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
            var result = await new AdapterSmokeTester(httpClient).RunAsync(
                port,
                apiKey,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromSeconds(2));
            WriteReport(reportPath,
                $"TEST PO RESTARCIE: SUKCES{Environment.NewLine}" +
                $"Czas: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}{Environment.NewLine}" +
                $"Instancja: {instanceName}{Environment.NewLine}" +
                $"Operacja: {intent.RootElement.GetProperty("operationId").GetGuid()}{Environment.NewLine}" +
                $"Szacowany start Windows: {thisBoot:O}{Environment.NewLine}" +
                $"Praca przed pierwszym logowaniem: wymaga niezależnego potwierdzenia; sam kontekst SYSTEM jej nie dowodzi.{Environment.NewLine}" +
                $"GET /health: OK{Environment.NewLine}" +
                $"GET /warehouses + API key + Sfera: OK{Environment.NewLine}" +
                $"Liczba magazynów: {result.WarehouseCount}{Environment.NewLine}");
        }
        catch (Exception exception)
        {
            Environment.ExitCode = 1;
            WriteReport(reportPath,
                $"TEST PO RESTARCIE: BŁĄD{Environment.NewLine}" +
                $"Czas: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}{Environment.NewLine}" +
                $"Instancja: {instanceName}{Environment.NewLine}" +
                $"Błąd: {exception.GetType().Name} (0x{exception.HResult:X8}){Environment.NewLine}" +
                $"Szczegóły: {FormatException(exception)}{Environment.NewLine}");
        }
        finally
        {
            AdapterScheduledTaskInstaller.DeleteTask(GetVerifierTaskName(instanceName));
        }
    }

    public async Task ShowResultAsync(string instanceName)
    {
        var reportPath = GetReportPath(instanceName);
        var shownMarkerPath = GetShownMarkerPath(reportPath);
        if (File.Exists(shownMarkerPath))
        {
            AdapterScheduledTaskInstaller.DeleteTask(GetViewerTaskName(instanceName));
            return;
        }
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(7);
        while (!File.Exists(reportPath) && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(TimeSpan.FromSeconds(2));

        var report = File.Exists(reportPath) ? File.ReadAllText(reportPath) : null;
        var result = PostRebootResultPresentation.Create(instanceName, reportPath, report);
        try
        {
            new PostRebootResultWindow(result).ShowDialog();
        }
        finally
        {
            Lifecycle.DeploymentStore.AtomicWrite(shownMarkerPath, DateTimeOffset.Now.ToString("O"));
            AdapterScheduledTaskInstaller.DeleteTask(GetViewerTaskName(instanceName));
        }
    }

    public static void RestartComputer()
    {
        var shutdown = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shutdown.exe");
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = shutdown,
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList = { "/r", "/t", "15", "/c", "Test adaptera SubiektNexoConnector po restarcie" }
        }) ?? throw new InvalidOperationException("Nie udało się zaplanować ponownego uruchomienia komputera.");
        if (!process.WaitForExit(10000)) throw new TimeoutException("Windows nie potwierdził zaplanowania restartu w ciągu 10 sekund.");
        if (process.ExitCode != 0)
            throw new InvalidOperationException("System Windows odrzucił żądanie ponownego uruchomienia.");
    }

    public static string? TryGetInstance(string[] args)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (args[index].Equals(InstanceArgument, StringComparison.OrdinalIgnoreCase))
                return args[index + 1];
        }
        return null;
    }

    public static string GetReportPath(string instanceName)
    {
        var instanceDirectory = Path.GetDirectoryName(InstanceSettingsWriter.GetSettingsPath(instanceName))!;
        return Path.Combine(instanceDirectory, "post-reboot-test.txt");
    }

    public static string GetVerifierTaskName(string instanceName) => $"Post-reboot test - {instanceName}";
    public static string GetViewerTaskName(string instanceName) => $"Post-reboot result - {instanceName}";

    private static (int Port, string ApiKey) ReadConnectionSettings(string instanceName)
    {
        var settingsPath = InstanceSettingsWriter.GetSettingsPath(instanceName);
        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
        var root = document.RootElement;
        var urls = root.GetProperty("Urls").GetString()
            ?? throw new InvalidOperationException("settings.json nie zawiera adresu Urls.");
        if (!Uri.TryCreate(urls, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("settings.json zawiera niepoprawny adres Urls.");
        var apiKey = root.GetProperty("Auth").GetProperty("ApiKey").GetString();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("settings.json nie zawiera API key.");
        return (uri.Port, apiKey);
    }

    private static void WriteReport(string reportPath, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        Lifecycle.DeploymentStore.AtomicWrite(reportPath, contents);
    }

    private static string GetShownMarkerPath(string reportPath) => reportPath + ".shown";

    private static string FormatException(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(current.Message) && !messages.Contains(current.Message))
                messages.Add(current.Message);
        }
        return string.Join(" | ", messages);
    }
}
