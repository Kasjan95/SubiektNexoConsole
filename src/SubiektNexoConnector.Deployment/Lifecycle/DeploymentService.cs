using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using SubiektNexoConnector.Deployment.Configuration;
using SubiektNexoConnector.Deployment.Nexo;
using SubiektNexoConnector.Deployment.Packaging;
using SubiektNexoConnector.Deployment.Testing;
using SubiektNexoConnector.Deployment.Windows;

namespace SubiektNexoConnector.Deployment.Lifecycle;

public sealed partial class DeploymentService : IAsyncDisposable
{
    public static string InstancesRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SubiektNexoConnector", "instances");
    private NexoWorker? _nexo;
    private DeploymentRequest? _request;
    private string? _staging;
    private readonly PortAvailabilityChecker _ports = new();
    public DeploymentState? State { get; private set; }
    public event EventHandler<StepRecord>? Progress;
    public event EventHandler<string>? TestProgress;
    public static DeploymentStore Store(string instance) => new(Path.Combine(Path.GetDirectoryName(InstanceSettingsWriter.GetSettingsPath(instance))!, "maintenance"), instance);

    public async Task<IReadOnlyList<NexoDatabaseInfo>> PrepareAsync(DeploymentRequest request, bool sqlWindowsAuth,
        string? sqlUser, string? sqlPassword, DeploymentState? existing)
    {
        request.Validate(File.Exists(InstanceSettingsWriter.GetSettingsPath(request.Instance)));
        _request = request;
        var launcher = Path.GetFullPath(request.LauncherPath);
        SafePath.EnsureNoReparsePoints(launcher);
        if (!File.Exists(launcher) || !Path.GetFileName(launcher).Equals("InsLauncher.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Wskaż zainstalowany InsLauncher.exe.");
        TrustedLauncher.Validate(launcher);
        var info = FileVersionInfo.GetVersionInfo(launcher);
        if (info.CompanyName?.Contains("InsERT", StringComparison.OrdinalIgnoreCase) != true)
            throw new InvalidOperationException("Nie rozpoznano producenta InsLauncher.exe.");
        if (existing is null && File.Exists(InstanceSettingsWriter.GetSettingsPath(request.Instance)))
            throw new InvalidOperationException("Wykryto starszą instalację bez rejestru. Wybierz przejęcie instalacji pilota; nie nadpisano jej konfiguracji.");
        if (existing is not null && (!request.Server.Equals(existing.Server, StringComparison.OrdinalIgnoreCase)
            || !request.Instance.Equals(existing.Instance, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Naprawa nie może zmieniać serwera ani tożsamości instancji.");
        if (existing is null || request.Port != existing.Port) await _ports.WaitUntilAvailableAsync(request.Port, TimeSpan.Zero);
        State = existing ?? new DeploymentState { Instance = request.Instance.ToLowerInvariant(), Server = request.Server.Trim(), LauncherPath = launcher };
        if (existing is not null)
        {
            var path = SafePath.Child(Store(existing.Instance).DirectoryPath, existing.PackagePath);
            if (!File.Exists(path) || Hash(path) != existing.PackageSha256)
                throw new InvalidDataException("Brak oryginalnego MPKG lub jego suma nie pasuje. Przywróć zweryfikowaną kopię pakietu przed naprawą.");
        }
        else
        {
            var embedded = new EmbeddedAdapterPayload();
            var publish = request.PublishDirectory;
            if (embedded.IsAvailable) { _staging = embedded.ExtractToTemporaryDirectory(); publish = _staging; }
            if (string.IsNullOrWhiteSpace(publish)) throw new InvalidOperationException("Instalator nie zawiera adaptera. Użyj opublikowanego Setup.exe.");
            var versionText = FileVersionInfo.GetVersionInfo(Path.Combine(publish, "SubiektNexoConnector.Api.dll")).FileVersion;
            var version = Version.Parse(versionText ?? throw new InvalidDataException("Brak wersji adaptera."));
            State.AdapterVersion = DeploymentNames.NormalizeVersion(version);
            var temporary = Path.Combine(Path.GetTempPath(), "SubiektNexoConnector.Deployment", Guid.NewGuid().ToString("N"));
            ProtectedDirectory.Create(temporary);
            var package = await Task.Run(() => new MpkgBuilder().Build(new MpkgBuildRequest(publish,
                Path.Combine(temporary, DeploymentNames.GetMpkgFileName(State.Instance, version)), State.Instance, version, State.RequiredNexoVersion)));
            State.PackagePath = package.OutputPath; State.PackageIdentity = package.PackageIdentity; State.PackageSha256 = Hash(package.OutputPath);
        }
        _nexo = new NexoWorker(launcher);
        var sqlConnection = SqlConnectionParameters.Create(sqlWindowsAuth, sqlUser, sqlPassword);
        await _nexo.Invoke(d =>
        {
            d.SetServer(State.Server, sqlConnection.WindowsAuthentication, sqlConnection.User, sqlConnection.Password);
            return true;
        });
        return await _nexo.Invoke(d =>
        {
            var packageInfo = d.GetPackageInfo(State.PackagePath);
            if (packageInfo.Package.Equals("ERROR", StringComparison.OrdinalIgnoreCase)
                || !packageInfo.Scenarios.Contains(DeploymentNames.GetLaunchScenarioName(State.Instance), StringComparer.Ordinal))
                throw new InvalidDataException("InsERT nie potwierdził scenariusza przygotowanego pakietu.");
            return d.GetDatabases(State.PackagePath);
        });
    }
    public void SelectDatabase(string database)
    {
        if (State is null) throw new InvalidOperationException();
        if (State.Database.Length > 0 && !State.Database.Equals(database, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Naprawa nie może przełączyć podmiotu.");
        State.Database = database;
    }
    public void UpdateRequest(DeploymentRequest request)
    {
        request.Validate(File.Exists(InstanceSettingsWriter.GetSettingsPath(request.Instance)));
        _request = request;
    }
    public async Task InstallAsync(WindowsCredentials? credentials, CancellationToken token)
    {
        var state = State ?? throw new InvalidOperationException();
        if (state.PendingUpdate is not null) throw new InvalidOperationException("Najpierw wznów oczekującą aktualizację.");
        var request = _request!;
        // Recheck immediately before side effects: the file may have disappeared since preparation.
        request.Validate(File.Exists(InstanceSettingsWriter.GetSettingsPath(request.Instance)));
        if (state.Database.Length == 0) throw new InvalidOperationException("Wybierz podmiot.");
        if (credentials is null && !AdapterScheduledTaskInstaller.Matches(state.Instance, state.LauncherPath, state.AccountSid))
            throw new InvalidOperationException("Podaj poświadczenia konta Windows do utworzenia autostartu.");
        if (credentials is not null)
        {
            if (state.AccountSid.Length > 0 && state.AccountSid != credentials.Sid)
                throw new InvalidOperationException("Naprawa wymaga tego samego konta Windows co Nexo.");
            await Task.Run(() => AdapterScheduledTaskInstaller.ValidateCredentials(state.Instance, state.LauncherPath, credentials), token);
            state.Account = credentials.Account; state.AccountSid = credentials.Sid;
        }
        if (AdapterScheduledTaskInstaller.Exists(state.Instance)
            && !AdapterScheduledTaskInstaller.Matches(state.Instance, state.LauncherPath, state.AccountSid, requireEnabled: false))
            throw new InvalidOperationException("Zadanie o tej nazwie wskazuje inne konto lub program. Nie zostało nadpisane.");
        var oldPort = new InstanceSettingsWriter().ReadExisting(state.Instance)?.Port ?? state.Port;
        var directory = new ProgramDataAclInstaller().Install(state.Instance, state.Account);
        var store = Store(state.Instance);
        var cachedPackage = Path.Combine(store.DirectoryPath, Path.GetFileName(state.PackagePath));
        SafePath.Child(store.DirectoryPath, cachedPackage);
        if (!cachedPackage.Equals(state.PackagePath, StringComparison.OrdinalIgnoreCase)) File.Copy(state.PackagePath, cachedPackage, true);
        state.PackagePath = cachedPackage;
        if (Hash(cachedPackage) != state.PackageSha256) throw new InvalidDataException("Pakiet zmienił się od przygotowania.");
        var previous = store.Load();
        var uncertainDeploy = previous?.Steps.Any(s => s.Id == "package" && s.Status is StepStatus.Running or StepStatus.Failed) == true;
        state.OperationId = Guid.NewGuid(); state.Mode = previous is null ? "install" : "repair";
        state.Port = request.Port; state.DesktopShortcut = request.DesktopShortcut; state.SeqEnabled = request.SeqEnabled;
        state.RemoteAddresses = request.RemoteAddresses; state.FirewallProfiles = request.Profiles;
        var wasInstalled = state.PackageInstalled;
        var steps = new List<DeploymentStep>
        {
            new("stop", "Zatrzymanie poprzedniej instancji", async ct =>
            {
                if (oldPort > 0 && _ports.IsInUse(oldPort))
                {
                    if (!AdapterScheduledTaskInstaller.Matches(state.Instance, state.LauncherPath, state.AccountSid))
                        throw new InvalidOperationException("Nie potwierdzono właściciela zadania. Nie zatrzymano obcego procesu.");
                    await StopAsync(state.Instance, ct);
                    await _ports.WaitUntilAvailableAsync(oldPort, TimeSpan.FromSeconds(30), ct);
                }
                await _ports.WaitUntilAvailableAsync(state.Port, TimeSpan.Zero, ct);
            }),
            new("package", "Pakiet i podmiot Nexo", async ct =>
            {
                ct.ThrowIfCancellationRequested();
                var databases = await _nexo!.Invoke(d => d.GetDatabases(state.PackagePath));
                var database = databases.SingleOrDefault(d => d.Name == state.Database) ?? throw new InvalidOperationException("Podmiot nie jest już dostępny.");
                if (uncertainDeploy && database.HasPackage && !request.ReinstallPackage)
                    throw new InvalidOperationException("Poprzednie wdrożenie ma niepewny wynik. Pakiet jest przypisany; potwierdź jego wersję w Nexo przed przejęciem/naprawą.");
                if (request.ReinstallPackage || !wasInstalled || !database.HasPackage)
                    if (!await _nexo.Invoke(d => d.Deploy(state.PackagePath, state.Database))) throw new InvalidOperationException("InsERT nie potwierdził instalacji pakietu.");
                state.PackageInstalled = true;
            }),
            new("settings", "Konfiguracja i chroniony klucz API", ct =>
            {
                var settingsPath = InstanceSettingsWriter.GetSettingsPath(state.Instance);
                var existing = File.Exists(settingsPath) ? JsonNode.Parse(File.ReadAllText(settingsPath))! : null;
                var apiKey = existing?["Auth"]?["ApiKey"]?.GetValue<string>() ?? ApiKeyGenerator.Generate();
                var password = request.NexoPassword.Length > 0 ? request.NexoPassword : existing?["Nexo"]?["SystemLogin"]?["NexoPassword"]?.GetValue<string>() ?? "";
                new InstanceSettingsWriter().Write(new InstanceSettingsRequest(state.Instance, state.Port, apiKey,
                    request.NexoUser, password, request.SeqEnabled ? request.SeqUrl : "", state.Database, existing is not null));
                new ApiKeyFileWriter().Write(store.DirectoryPath, state.Instance, apiKey, true);
                return Task.CompletedTask;
            }),
            new("firewall", "Ograniczenie dostępu sieciowego", ct => Task.Run(() =>
                new FirewallRuleInstaller().Install(state.Instance, state.Port, state.RemoteAddresses, state.FirewallProfiles), ct),
                ct => Task.FromResult(new FirewallRuleInstaller().Matches(state.Instance, state.Port, state.RemoteAddresses, state.FirewallProfiles))),
            new("task", "Autostart bez zalogowanego użytkownika", ct =>
            {
                if (credentials is not null) credentials.Use(password => new AdapterScheduledTaskInstaller().Install(state.Instance, state.LauncherPath, state.Account, password));
                return Task.CompletedTask;
            }, ct => Task.FromResult(AdapterScheduledTaskInstaller.Matches(state.Instance, state.LauncherPath, state.AccountSid))),
            new("shortcut", "Skrót na wspólnym pulpicie (opcjonalny)", ct =>
            {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                if (request.DesktopShortcut) new InsLauncherShortcutInstaller().Install(state.Instance, state.LauncherPath, desktop);
                else InsLauncherShortcutInstaller.RemoveOwned(Path.Combine(desktop, DeploymentNames.GetLaunchScenarioName(state.Instance) + ".lnk"), state.Instance, state.LauncherPath);
                return Task.CompletedTask;
            }, Skip: !request.DesktopShortcut && previous?.DesktopShortcut != true, Optional: true),
            new("registration", "Rejestracja narzędzia naprawy i odinstalowania", ct => { MaintenanceRegistration.Register(state); return Task.CompletedTask; }),
            new("start", "Uruchomienie scenariusza InsLauncher", ct =>
            {
                if (System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value == state.AccountSid)
                    new InsLauncherScenarioStarter().Start(state.Instance, state.LauncherPath);
                else AdapterScheduledTaskInstaller.StartAdapterTask(state.Instance);
                return Task.CompletedTask;
            }),
            new("test", "Health, klucz API i Sfera", TestAsync),
            new("batch-test", "Test pełnego zadania w kontekście konta Windows", async ct =>
            {
                await StopAsync(state.Instance, ct);
                await _ports.WaitUntilAvailableAsync(state.Port, TimeSpan.FromSeconds(45), ct);
                AdapterScheduledTaskInstaller.StartAdapterTask(state.Instance);
                await TestAsync(ct);
            }),
            new("seq", "Dostępność Seq (opcjonalna)", async ct =>
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                using var response = await http.GetAsync(request.SeqUrl, ct);
                response.EnsureSuccessStatusCode();
            }, Skip: !request.SeqEnabled, Optional: true)
        };
        // A new attempt deliberately re-evaluates every local step; only external uncertain writes are guarded separately.
        state.Steps = [];
        var engine = new DeploymentOrchestrator(store);
        engine.Progress += (_, step) => { InstallerLog.Event($"step:{step.Id}:{step.Status}"); Progress?.Invoke(this, step); };
        await engine.RunAsync(state, steps, token);
    }
    public async Task TestAsync(CancellationToken token)
    {
        var state = State ?? throw new InvalidOperationException();
        var existing = new InstanceSettingsWriter().ReadExisting(state.Instance) ?? throw new InvalidOperationException("Brak konfiguracji.");
        using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        var tester = new AdapterSmokeTester(http);
        tester.StagePassed += (_, stage) => TestProgress?.Invoke(this, stage);
        await tester.RunAsync(existing.Port, existing.ApiKey, TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(2), token);
        using var identityDeadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        identityDeadline.CancelAfter(TimeSpan.FromSeconds(10));
        await new AdapterControl(http).VerifyIdentityAsync(existing.Port, existing.ApiKey, state.Instance, identityDeadline.Token);
    }
    public async Task RetryVerificationAsync(CancellationToken token)
    {
        var state = State ?? throw new InvalidOperationException();
        if (state.PendingUpdate is not null) throw new InvalidOperationException("Wybierz Aktualizuj / wznów, aby zweryfikować i zatwierdzić docelową wersję.");
        var required = new[] { "package", "settings", "firewall", "task", "registration", "start" };
        if (state.Mode is "uninstall" or "uninstall-local" || required.Any(id => !state.Steps.Any(s => s.Id == id && s.Status == StepStatus.Completed)))
        {
            await TestAsync(token);
            return; // A healthy HTTP process cannot complete missing installation work.
        }
        var engine = new DeploymentOrchestrator(Store(state.Instance));
        engine.Progress += (_, step) => Progress?.Invoke(this, step);
        await engine.RunAsync(state,
        [new("test", "Health, klucz API i Sfera", TestAsync),
         new("batch-test", "Test pełnego zadania Windows", async ct =>
         {
             if (!AdapterScheduledTaskInstaller.Matches(state.Instance, state.LauncherPath, state.AccountSid)) throw new InvalidOperationException("Autostart wymaga naprawy.");
             await StopAsync(state.Instance, ct);
             await _ports.WaitUntilAvailableAsync(state.Port, TimeSpan.FromSeconds(45), ct);
             AdapterScheduledTaskInstaller.StartAdapterTask(state.Instance);
             await TestAsync(ct);
         })], token);
    }
    private static async Task StopAsync(string instance, CancellationToken token)
    {
        var settings = new InstanceSettingsWriter().ReadExisting(instance) ?? throw new InvalidOperationException("Brak konfiguracji zatrzymywanej instancji.");
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        await new AdapterControl(http).StopAsync(settings.Port, settings.ApiKey, instance, token);
    }
    public void UseState(DeploymentState state) => State = state;
    public async Task UninstallAsync(bool localOnly, bool deleteData, CancellationToken token)
    {
        var state = State ?? throw new InvalidOperationException();
        state.Mode = localOnly ? "uninstall-local" : "uninstall"; state.Steps = []; state.OperationId = Guid.NewGuid();
        var runningPort = new InstanceSettingsWriter().ReadExisting(state.Instance)?.Port ?? state.Port;
        var store = Store(state.Instance);
        var engine = new DeploymentOrchestrator(store);
        engine.Progress += (_, step) => Progress?.Invoke(this, step);
        await engine.RunAsync(state, new DeploymentStep[]
        {
            new("stop", "Zatrzymanie instancji", async ct =>
            {
                if (_ports.IsInUse(runningPort))
                {
                    if (!AdapterScheduledTaskInstaller.Matches(state.Instance, state.LauncherPath, state.AccountSid)) throw new InvalidOperationException("Nie potwierdzono właściciela procesu.");
                    await StopAsync(state.Instance, ct);
                    await _ports.WaitUntilAvailableAsync(runningPort, TimeSpan.FromSeconds(30), ct);
                }
            }),
            new("detach", "Odłączenie pakietu od podmiotu", async ct =>
            {
                if (_nexo is null) throw new InvalidOperationException("Połącz z serwerem, aby odłączyć pakiet.");
                var packages = new List<AdapterPackageReference>();
                foreach (var package in AdapterUpdatePolicy.GetDetachmentPackages(state))
                {
                    SafePath.Child(store.DirectoryPath, package.PackagePath);
                    if (!File.Exists(package.PackagePath) || Hash(package.PackagePath) != package.Sha256)
                    {
                        if (package.Required)
                            throw new InvalidDataException("Brak zweryfikowanego MPKG potrzebnego do odinstalowania. Przywróć właściwy pakiet z kopii.");
                        continue;
                    }
                    packages.Add(package);
                }

                foreach (var package in packages)
                {
                    var databases = await _nexo.Invoke(d => d.GetDatabases(package.PackagePath));
                    var db = databases.SingleOrDefault(d => d.Name == state.Database)
                        ?? throw new InvalidOperationException("Nie potwierdzono podmiotu; odłączenie nie zostało wykonane.");
                    if (db.HasPackage && !await _nexo.Invoke(d => d.Undeploy(package.PackagePath, state.Database)))
                        throw new InvalidOperationException("Nexo nie potwierdziło odłączenia pakietu.");
                }

                foreach (var package in packages)
                {
                    var after = await _nexo.Invoke(d => d.GetDatabases(package.PackagePath));
                    if (after.Any(d => d.Name == state.Database && d.HasPackage))
                        throw new InvalidOperationException("Pakiet pozostaje przypisany do podmiotu.");
                }
                state.PackageInstalled = false;
                state.PendingUpdate = null;
            }, Skip: localOnly),
            new("local", "Usunięcie autostartu, reguły i skrótu", ct =>
            {
                if (AdapterScheduledTaskInstaller.Exists(state.Instance)
                    && !AdapterScheduledTaskInstaller.Matches(state.Instance, state.LauncherPath, state.AccountSid, requireEnabled: false))
                    throw new InvalidOperationException("Zadanie zostało zmienione poza instalatorem. Nie usunięto obcego zasobu.");
                AdapterScheduledTaskInstaller.DeleteTask(AdapterScheduledTaskInstaller.GetAdapterTaskName(state.Instance));
                AdapterScheduledTaskInstaller.DeleteTask(PostRebootTestCoordinator.GetVerifierTaskName(state.Instance));
                AdapterScheduledTaskInstaller.DeleteTask(PostRebootTestCoordinator.GetViewerTaskName(state.Instance));
                new FirewallRuleInstaller().Remove(state.Instance);
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                var shortcut = SafePath.Child(desktop, Path.Combine(desktop, DeploymentNames.GetLaunchScenarioName(state.Instance) + ".lnk"));
                InsLauncherShortcutInstaller.RemoveOwned(shortcut, state.Instance, state.LauncherPath);
                if (!localOnly) MaintenanceRegistration.Remove(state.Instance);
                return Task.CompletedTask;
            })
        }, token);
        if (localOnly) { state.Status = OperationStatus.PendingServerRemoval; store.Save(state); }
        else if (deleteData) SafePath.DeleteTree(InstancesRoot, Path.GetDirectoryName(InstanceSettingsWriter.GetSettingsPath(state.Instance))!);
    }
    public static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    public async ValueTask DisposeAsync()
    {
        if (_nexo is not null) await _nexo.DisposeAsync();
        if (_staging is not null)
        {
            try { SafePath.DeleteTree(Path.Combine(Path.GetTempPath(), "SubiektNexoConnector.Deployment"), _staging); }
            catch (Exception e) { InstallerLog.Event("cleanup.failed", e); }
        }
    }
}
