using System.Diagnostics;
using System.IO;
using System.Net.Http;
using SubiektNexoConnector.Deployment.Configuration;
using SubiektNexoConnector.Deployment.Nexo;
using SubiektNexoConnector.Deployment.Packaging;
using SubiektNexoConnector.Deployment.Testing;
using SubiektNexoConnector.Deployment.Windows;

namespace SubiektNexoConnector.Deployment.Lifecycle;

public sealed partial class DeploymentService
{
    private PendingAdapterUpdate? _preparedUpdate;
    public string UpdateDescription => $"{State?.AdapterVersion} → {_preparedUpdate?.Version}\nScenariusz i podmiot pozostają bez zmian.";

    public async Task<IReadOnlyList<NexoDatabaseInfo>> PrepareUpdateAsync(DeploymentRequest request, bool windowsAuth,
        string? sqlUser, string? sqlPassword, DeploymentState existing)
    {
        if (existing.Status is OperationStatus.Removed or OperationStatus.PendingServerRemoval)
            throw new InvalidOperationException("Nie można aktualizować odinstalowanej instancji.");
        if (new InstanceSettingsWriter().ReadExisting(existing.Instance) is null)
            throw new InvalidOperationException("Przed aktualizacją przywróć settings.json. Aktualizacja nie generuje nowego klucza ani konfiguracji.");
        var databases = await PrepareAsync(request, windowsAuth, sqlUser, sqlPassword, existing);
        var original = PilotImporter.Inspect(existing.PackagePath);
        if (original.Identity != existing.PackageIdentity || !original.Instance.Equals(existing.Instance, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Oryginalny manifest nie pasuje do zarejestrowanej instancji.");
        if (existing.PendingUpdate is { } pending)
        {
            SafePath.Child(Store(existing.Instance).DirectoryPath, pending.PackagePath);
            if (!File.Exists(pending.PackagePath) || Hash(pending.PackagePath) != pending.Sha256)
                throw new InvalidDataException("Brak zweryfikowanego pakietu rozpoczętej aktualizacji. Przywróć jego kopię; nie zastępuj innym wydaniem.");
            _preparedUpdate = pending;
        }
        else
        {
            var embedded = new EmbeddedAdapterPayload();
            var publish = request.PublishDirectory;
            if (embedded.IsAvailable) { _staging = embedded.ExtractToTemporaryDirectory(); publish = _staging; }
            if (string.IsNullOrWhiteSpace(publish)) throw new InvalidOperationException("Brak nowego payloadu. Uruchom nowszy Setup.exe.");
            var info = FileVersionInfo.GetVersionInfo(Path.Combine(publish, "SubiektNexoConnector.Api.dll"));
            var target = Version.Parse(info.FileVersion ?? throw new InvalidDataException("Brak wersji adaptera."));
            _ = AdapterUpdatePolicy.NextIdentity(existing.PackageIdentity, target);
            if (target <= Version.Parse(existing.AdapterVersion)) throw new InvalidOperationException("Payload nie jest nowszy od zainstalowanego adaptera.");
            var directory = Path.Combine(Path.GetTempPath(), "SubiektNexoConnector.Deployment", Guid.NewGuid().ToString("N"));
            ProtectedDirectory.Create(directory);
            var result = await Task.Run(() => new MpkgBuilder().Build(new MpkgBuildRequest(publish,
                Path.Combine(directory, DeploymentNames.GetMpkgFileName(existing.Instance, target)), existing.Instance,
                target, existing.RequiredNexoVersion, ExistingPackageIdentity: existing.PackageIdentity)));
            _preparedUpdate = new PendingAdapterUpdate
            {
                Version = DeploymentNames.NormalizeVersion(target), InformationalVersion = info.ProductVersion ?? "",
                PackagePath = result.OutputPath, PackageIdentity = result.PackageIdentity, Sha256 = Hash(result.OutputPath)
            };
        }
        var packageInfo = await _nexo!.Invoke(d => d.GetPackageInfo(_preparedUpdate.PackagePath));
        if (packageInfo.Package.Equals("ERROR", StringComparison.OrdinalIgnoreCase)
            || !packageInfo.Scenarios.Contains(DeploymentNames.GetLaunchScenarioName(existing.Instance), StringComparer.Ordinal))
            throw new InvalidDataException("InsERT nie potwierdził scenariusza nowego MPKG.");
        return databases;
    }

    public async Task UpdateAsync(bool confirmUncertainDeploy, CancellationToken token)
    {
        var state = State ?? throw new InvalidOperationException();
        var update = _preparedUpdate ?? throw new InvalidOperationException("Najpierw przygotuj aktualizację.");
        var settings = new InstanceSettingsWriter().ReadExisting(state.Instance) ?? throw new InvalidOperationException("Brak konfiguracji.");
        if (!AdapterScheduledTaskInstaller.Matches(state.Instance, state.LauncherPath, state.AccountSid))
            throw new InvalidOperationException("Przed aktualizacją napraw pełne zadanie autostartu dla zapisanego konta Windows.");
        var store = Store(state.Instance);
        SafePath.EnsureNoReparsePoints(update.PackagePath);
        if (Hash(update.PackagePath) != update.Sha256) throw new InvalidDataException("Pakiet aktualizacji został zmieniony.");
        var destination = SafePath.Child(store.DirectoryPath, Path.Combine(store.DirectoryPath, Path.GetFileName(update.PackagePath)));
        if (!destination.Equals(update.PackagePath, StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(destination))
            {
                if (Hash(destination) != update.Sha256) throw new InvalidDataException("Inne binaria mają tę samą wersję w cache. Nie nadpisano pakietu.");
            }
            else File.Copy(update.PackagePath, destination, false);
        }
        update.PackagePath = destination;
        var uncertain = state.Steps.Any(s => s.Id == "update-package" && s.Status is StepStatus.Running or StepStatus.Failed);
        if (uncertain && !update.Deployed && !confirmUncertainDeploy)
            throw new InvalidOperationException("Wynik poprzedniego Deploy jest niepewny. Potwierdź w podsumowaniu ponowne wgranie tego samego MPKG.");
        if (state.PendingUpdate is null) { state.Steps = []; state.OperationId = Guid.NewGuid(); }
        state.PendingUpdate = update; state.Mode = "update";
        // Persist target before any stop/deploy. The installed fields still describe the last verified release.
        store.Save(state);
        var engine = new DeploymentOrchestrator(store);
        engine.Progress += (_, step) => Progress?.Invoke(this, step);
        await engine.RunAsync(state,
        [
            new("update-backup", "Kopia niezmienianej konfiguracji", ct =>
            {
                var backup = Path.Combine(store.DirectoryPath, $"settings-before-update-{state.OperationId:N}.json");
                if (!File.Exists(backup)) DeploymentStore.AtomicWrite(backup, File.ReadAllText(InstanceSettingsWriter.GetSettingsPath(state.Instance)));
                return Task.CompletedTask;
            }),
            new("update-stop", "Zatrzymanie adaptera przed aktualizacją", async ct =>
            {
                if (!_ports.IsInUse(settings.Port)) return;
                try { await StopAsync(state.Instance, ct); }
                catch (InvalidOperationException e)
                {
                    throw new InvalidOperationException("Nie potwierdzono bezpiecznego zatrzymania. Jeśli to stary pilot bez maintenance/info, zamknij adapter ręcznie i wznów aktualizację; nie odinstalowuj go.", e);
                }
                await _ports.WaitUntilAvailableAsync(settings.Port, TimeSpan.FromSeconds(45), ct);
            }),
            new("update-idempotency-storage", "Konfiguracja lokalnych baz danych", ct =>
            {
                new ProgramDataAclInstaller().Install(state.Instance, state.Account);
                new InstanceSettingsWriter().EnsurePersistenceConfiguration(state.Instance);
                return Task.CompletedTask;
            }),
            new("update-package", "Aktualizacja pakietu wybranego podmiotu", async ct =>
            {
                ct.ThrowIfCancellationRequested();
                if (Hash(update.PackagePath) != update.Sha256) throw new InvalidDataException("Pakiet aktualizacji został zmieniony.");
                if (!update.Deployed)
                {
                    if (!await _nexo!.Invoke(d => d.Deploy(update.PackagePath, state.Database)))
                        throw new InvalidOperationException("InsERT nie potwierdził aktualizacji. Nie ogłoszono sukcesu ani nie wykonano rollbacku.");
                    update.Deployed = true; store.Save(state);
                }
            }),
            new("update-start", "Uruchomienie przez istniejące zadanie Windows", ct =>
            {
                AdapterScheduledTaskInstaller.StartAdapterTask(state.Instance); return Task.CompletedTask;
            }),
            new("update-smoke-test", "Health, API key i Sfera po aktualizacji", TestAsync),
            new("update-version-test", "Tożsamość i wersja uruchomionych binariów", async ct =>
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                await new AdapterControl(http).VerifyVersionAsync(settings.Port, settings.ApiKey, state.Instance,
                    update.Version, update.InformationalVersion, ct);
            }),
            new("update-commit", "Zatwierdzenie sprawdzonej wersji", ct =>
            {
                AdapterUpdatePolicy.CommitVerified(state);
                MaintenanceRegistration.Register(state);
                return Task.CompletedTask;
            })
        ], token);
        state.PendingUpdate = null;
        store.Save(state);
    }
}
