using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using Microsoft.Win32;
using SubiektNexoConnector.Deployment.Configuration;
using SubiektNexoConnector.Deployment.Lifecycle;
using SubiektNexoConnector.Deployment.Packaging;
using SubiektNexoConnector.Deployment.Testing;
using SubiektNexoConnector.Deployment.Windows;

namespace SubiektNexoConnector.Deployment;

public partial class MainWindow : Window
{
    private DeploymentService? _service;
    private DeploymentState? _existing;
    private WindowsCredentials? _credentials;
    private CancellationTokenSource? _operation;
    private bool _busy;
    private int _page;
    private bool _removing;
    private bool _updating;
    private string? _importPackage;
    private sealed record InstanceListItem(string Instance, DeploymentState? State, bool CanImport, string Display);
    public MainWindow(bool preview = false)
    {
        InitializeComponent();
        foreach (var control in new System.Windows.Controls.Control[] { InstanceBox, ServerBox, PortBox, NexoUserBox, NexoPasswordBox, SeqBox, AddressesBox })
            control.LostFocus += ValidateField;
        Width = Math.Min(Width, SystemParameters.WorkArea.Width - 32);
        Height = Math.Min(Height, SystemParameters.WorkArea.Height - 32);
        LauncherBox.Text = InsLauncherShortcutInstaller.GetDefaultLauncherPath();
        if (new EmbeddedAdapterPayload().IsAvailable) PublishBox.Visibility = PublishLabel.Visibility = Visibility.Collapsed;
        ActivityText.Text = "Gotowy. Wybierz tryb pracy.\n";
        ShowPage(0); if (!preview) RefreshInstances();
    }
    private void ShowPage(int page)
    {
        _page = page;
        var panels = new[] { HomePanel, MaintenancePanel, ConnectionPanel, OptionsPanel, SummaryPanel, ProgressPanel };
        for (var index = 0; index < panels.Length; index++) panels[index].Visibility = index == page ? Visibility.Visible : Visibility.Collapsed;
        TitleText.Text = new[] { "Wybierz tryb", "Naprawa i aktualizacja", "Połączenie z Nexo", "Dostęp i integracje", "Podsumowanie", "Wynik" }[page];
        SubtitleText.Text = page == 0 ? "Zainstaluj nowy adapter lub zarządzaj istniejącym." : "Ustawienia i postęp dotyczą jednej wybranej instancji.";
        NavigationText.Text = string.Join("\n", new[] { "Tryb", "Serwis", "Podmiot", "Dostęp", "Podsumowanie", "Wynik" }
            .Select((title, index) => $"{(index == page ? "●" : (index + 1).ToString())}  {title}"));
        BackButton.Visibility = page is 0 or 5 ? Visibility.Collapsed : Visibility.Visible;
    }
    private void RefreshInstances()
    {
        var instances = new List<InstanceListItem>();
        if (Directory.Exists(DeploymentService.InstancesRoot))
            foreach (var directory in Directory.EnumerateDirectories(DeploymentService.InstancesRoot))
            {
                try
                {
                    SafePath.EnsureNoReparsePoints(directory);
                    var instance = Path.GetFileName(directory);
                    var state = DeploymentService.Store(instance).Load();
                    if (state is not null) instances.Add(new(instance, state, false, state.Display));
                    else if (File.Exists(Path.Combine(directory, "settings.json"))) instances.Add(new(instance, null, true, $"{instance} · starsza instalacja — do przejęcia"));
                }
                catch (Exception e) { instances.Add(new(Path.GetFileName(directory), null, false, Path.GetFileName(directory) + " · uszkodzony stan — zobacz przebieg")); InstallerLog.Event("discovery.failed", e); }
            }
        InstancesList.ItemsSource = instances.OrderBy(i => i.Instance).ToArray();
        EmptyInstancesText.Visibility = instances.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        InstancesList.SelectedIndex = -1;
        InstancesList_SelectionChanged(this, null!);
    }
    private InstanceListItem SelectedItem() => InstancesList.SelectedItem as InstanceListItem ?? throw new InvalidOperationException("Wybierz instalację z listy.");
    private DeploymentState Selected() => SelectedItem().State ?? throw new InvalidOperationException("Ta starsza instalacja wymaga najpierw przejęcia.");
    private void InstancesList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var selected = InstancesList.SelectedItem as InstanceListItem;
        HomeRepairButton.IsEnabled = selected is { State: not null } || selected?.CanImport == true;
        HomeRemoveButton.IsEnabled = selected?.State is not null;
    }
    private async Task ResetService()
    {
        if (_service is not null) await _service.DisposeAsync();
        _service = new DeploymentService();
        _service.Progress += (_, step) => Dispatcher.Invoke(() => { RefreshProgress(); AppendActivity($"{step.Title}: {StepLabel(step.Status)}"); });
        _service.TestProgress += (_, stage) => Dispatcher.Invoke(() => { ResultText.Text = stage == "health" ? "HTTP działa. Otwieranie sesji Sfery…" : "API key i Sfera działają."; AppendActivity(ResultText.Text); });
    }
    private async Task Execute(Func<CancellationToken, Task> action)
    {
        if (_busy) return;
        _busy = true; _operation = new CancellationTokenSource();
        BackButton.IsEnabled = false; ResultActions.IsEnabled = false;
        HomePanel.IsEnabled = MaintenancePanel.IsEnabled = ConnectionPanel.IsEnabled = OptionsPanel.IsEnabled = SummaryPanel.IsEnabled = false;
        CancelButton.Visibility = BusyProgress.Visibility = Visibility.Visible;
        ErrorPanel.Visibility = Visibility.Collapsed;
        try { await action(_operation.Token); }
        catch (OperationCanceledException) { ShowError("Anulowano. Wykonane etapy zachowano; możesz wznowić pracę z listy instancji."); }
        catch (Exception e) { InstallerLog.Event("operation.failed", e); ShowError($"{e.Message}\nKod: 0x{e.HResult:X8}. Raport: {InstallerLog.PathName}"); }
        finally
        {
            _operation.Dispose(); _operation = null; _busy = false;
            BackButton.IsEnabled = ResultActions.IsEnabled = true;
            HomePanel.IsEnabled = MaintenancePanel.IsEnabled = ConnectionPanel.IsEnabled = OptionsPanel.IsEnabled = SummaryPanel.IsEnabled = true;
            CancelButton.Visibility = BusyProgress.Visibility = Visibility.Collapsed;
            PurgeButton.Visibility = _service?.State?.Status == OperationStatus.Removed ? Visibility.Visible : Visibility.Collapsed;
            RetryTestButton.IsEnabled = _service?.State is { Status: not OperationStatus.Removed and not OperationStatus.PendingServerRemoval };
        }
    }
    private void ShowError(string message) { ErrorText.Text = message; ErrorPanel.Visibility = Visibility.Visible; AppendActivity("BŁĄD: " + message.Split('\n')[0]); }
    private void AppendActivity(string message)
    {
        ActivityText.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        ActivityText.ScrollToEnd();
    }
    private static string StepLabel(StepStatus status) => status switch
    { StepStatus.Completed => "gotowe", StepStatus.Failed => "błąd", StepStatus.Skipped => "pominięto", StepStatus.Running => "trwa", _ => "oczekuje" };
    private void ValidateField(object sender, RoutedEventArgs e)
    {
        if (_busy || sender is not System.Windows.Controls.Control control) return;
        var fields = new Dictionary<string, string> { ["InstanceBox"] = "Instance", ["ServerBox"] = "Server", ["PortBox"] = "Port",
            ["NexoUserBox"] = "NexoUser", ["NexoPasswordBox"] = "NexoPassword", ["SeqBox"] = "SeqUrl", ["AddressesBox"] = "RemoteAddresses" };
        var errors = Request().GetValidationErrors(HasSettings());
        if (errors.TryGetValue(fields[control.Name], out var error))
        {
            control.BorderBrush = System.Windows.Media.Brushes.Firebrick; control.ToolTip = error;
            System.Windows.Automation.AutomationProperties.SetHelpText(control, error);
        }
        else
        {
            control.ClearValue(System.Windows.Controls.Control.BorderBrushProperty); control.ClearValue(ToolTipProperty);
            System.Windows.Automation.AutomationProperties.SetHelpText(control, "");
        }
    }
    private async void New_Click(object sender, RoutedEventArgs e) => await Execute(async ct =>
    {
        _updating = false; RetryUpdateCheck.Visibility = Visibility.Collapsed; ExecuteButton.Content = "Zainstaluj / napraw";
        await ResetService(); _existing = null; _removing = false; _credentials?.Dispose(); _credentials = null;
        _importPackage = null;
        LocalRemoveButton.Visibility = Visibility.Collapsed;
        InstanceBox.IsReadOnly = ServerBox.IsReadOnly = false; InstanceBox.Text = "pilot-01";
        SeqCheck.IsChecked = false; ShortcutCheck.IsChecked = true; RebootCheck.IsChecked = false; ReinstallCheck.IsChecked = false; SqlWindowsCheck.IsChecked = false;
        SqlUserBox.Text = "sa"; SqlPasswordBox.Clear(); NexoPasswordBox.Clear(); AccountText.Text = "Wybierz konto Windows."; ShowPage(2);
    });
    private void Repair_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedItem();
        MaintenanceInstanceText.Text = selected.Display;
        var managed = selected.State is not null;
        MaintenanceCheckButton.IsEnabled = MaintenanceRepairButton.IsEnabled = MaintenanceUpdateButton.IsEnabled = managed;
        MaintenanceRestorePackageButton.IsEnabled = MaintenanceRestoreSettingsButton.IsEnabled = managed;
        MaintenanceImportButton.IsEnabled = selected.CanImport;
        ShowPage(1);
    }
    private async void RepairSelected_Click(object sender, RoutedEventArgs e) => await EditSelected(false);
    private async void Update_Click(object sender, RoutedEventArgs e) => await EditSelected(false, true);
    private async void Remove_Click(object sender, RoutedEventArgs e) => await EditSelected(true);
    private async Task EditSelected(bool remove, bool update = false) => await Execute(async ct =>
    {
        _existing = Selected(); _removing = remove; await ResetService(); _service!.UseState(_existing);
        _updating = !remove && (update || _existing.PendingUpdate is not null);
        RetryUpdateCheck.IsChecked = false;
        RetryUpdateCheck.Visibility = _updating ? Visibility.Visible : Visibility.Collapsed;
        ExecuteButton.Content = _updating ? "Aktualizuj" : "Zainstaluj / napraw";
        ReinstallCheck.IsChecked = false; SqlWindowsCheck.IsChecked = false;
        LocalRemoveButton.Visibility = remove ? Visibility.Visible : Visibility.Collapsed;
        _importPackage = null;
        _credentials?.Dispose(); _credentials = null;
        InstanceBox.Text = _existing.Instance; ServerBox.Text = _existing.Server;
        InstanceBox.IsReadOnly = ServerBox.IsReadOnly = true; LauncherBox.Text = _existing.LauncherPath;
        PortBox.Text = _existing.Port.ToString(); AddressesBox.Text = _existing.RemoteAddresses;
        PrivateProfileCheck.IsChecked = _existing.FirewallProfiles.Contains("private");
        DomainProfileCheck.IsChecked = _existing.FirewallProfiles.Contains("domain");
        PublicProfileCheck.IsChecked = _existing.FirewallProfiles.Contains("public");
        ShortcutCheck.IsChecked = _existing.DesktopShortcut; SeqCheck.IsChecked = _existing.SeqEnabled;
        AccountText.Text = _existing.Account;
        var settings = new InstanceSettingsWriter().ReadForEdit(_existing.Instance);
        NexoUserBox.Text = settings?["Nexo"]?["SystemLogin"]?["NexoUser"]?.GetValue<string>() ?? "";
        var sinks = settings?["Serilog"]?["WriteTo"] as JsonArray;
        SeqBox.Text = sinks?.FirstOrDefault(s => s?["Name"]?.GetValue<string>() == "Seq")?["Args"]?["serverUrl"]?.GetValue<string>() ?? "";
        SqlWindowsCheck.IsChecked = false; SqlUserBox.Text = "sa"; SqlPasswordBox.Clear(); NexoPasswordBox.Clear(); ShowPage(2);
        if (settings is null)
            SubtitleText.Text = "Brak settings.json. Podaj ponownie operatora i hasło Nexo oraz adres Seq, jeśli go używasz. Konfiguracja powstanie po wznowieniu. Jeśli była już używana, najpierw przywróć jej kopię zapasową, aby zachować ustawienia i klucz API.";
        if (remove) SubtitleText.Text = "Połącz z serwerem, aby bezpiecznie odłączyć pakiet od właściwego podmiotu.";
    });
    private bool HasSettings()
    {
        try { return File.Exists(InstanceSettingsWriter.GetSettingsPath(InstanceBox.Text.Trim())); }
        catch (InvalidOperationException) { return false; }
    }
    private void SqlWindowsCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (SqlCredentialsPanel is not null) SqlCredentialsPanel.Visibility = SqlWindowsCheck.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
    }
    private DeploymentRequest Request() => new()
    {
        Instance = InstanceBox.Text.Trim(),
        Server = ServerBox.Text.Trim(),
        LauncherPath = LauncherBox.Text.Trim(),
        PublishDirectory = PublishBox.Text.Trim(),
        Port = int.TryParse(PortBox.Text, out var port) ? port : 0,
        NexoUser = NexoUserBox.Text.Trim(),
        NexoPassword = NexoPasswordBox.Password,
        SeqEnabled = SeqCheck.IsChecked == true,
        SeqUrl = SeqBox.Text.Trim(),
        DesktopShortcut = ShortcutCheck.IsChecked == true,
        ReinstallPackage = ReinstallCheck.IsChecked == true,
        RemoteAddresses = AddressesBox.Text.Trim(),
        Profiles = string.Join(",", new[]
        { PrivateProfileCheck.IsChecked == true ? "private" : null, DomainProfileCheck.IsChecked == true ? "domain" : null,
            PublicProfileCheck.IsChecked == true ? "public" : null }.Where(p => p is not null))
    };
    private async void Connect_Click(object sender, RoutedEventArgs e) => await Execute(async ct =>
    {
        await ResetService();
        var windowsAuthentication = SqlWindowsCheck.IsChecked == true;
        var sqlUser = SqlUserBox.Text.Trim();
        var sqlPassword = SqlPasswordBox.Password;
        var sqlConnection = Nexo.SqlConnectionParameters.Create(windowsAuthentication, sqlUser, sqlPassword);
        AppendActivity($"Łączenie z serwerem {ServerBox.Text.Trim()}…");
        try
        {
            if (_importPackage is not null)
            {
                await using var worker = new Nexo.NexoWorker(LauncherBox.Text.Trim());
                await worker.Invoke(d => { d.SetServer(ServerBox.Text.Trim(), sqlConnection.WindowsAuthentication, sqlConnection.User, sqlConnection.Password); return true; });
                var candidates = await worker.Invoke(d => d.GetDatabases(_importPackage));
                var selector = new NexoDatabaseSelectionWindow(candidates.Where(d => d.HasPackage).ToArray(), "") { Owner = this };
                if (selector.ShowDialog() != true || selector.SelectedDatabase is null) throw new OperationCanceledException();
                using var account = WindowsCredentials.Prompt(this, "Potwierdź konto Windows zapisane w zadaniu pilota. Instalator przełączy autostart na pełne zadanie z hasłem.");
                _existing = PilotImporter.Import(_importPackage, ServerBox.Text.Trim(), LauncherBox.Text.Trim(), account, selector.SelectedDatabase.Name);
                _importPackage = null;
                AccountText.Text = _existing.Account;
            }
            var databases = _updating
                ? await _service!.PrepareUpdateAsync(Request(), windowsAuthentication, sqlUser, sqlPassword, _existing!)
                : await _service!.PrepareAsync(Request(), windowsAuthentication, sqlUser, sqlPassword, _existing);
            ct.ThrowIfCancellationRequested();
            if (_existing is not null)
            {
                if (!databases.Any(d => d.Name == _existing.Database)) throw new InvalidOperationException("Nie znaleziono zapisanego podmiotu na tym serwerze.");
                _service.SelectDatabase(_existing.Database);
            }
            else
            {
                var selector = new NexoDatabaseSelectionWindow(databases, "") { Owner = this };
                if (selector.ShowDialog() != true || selector.SelectedDatabase is null) throw new OperationCanceledException();
                _service.SelectDatabase(selector.SelectedDatabase.Name);
            }
            AppendActivity($"Wybrano podmiot: {_service.State!.Database}.");
            if (_updating)
            {
                SummaryText.Text = $"Aktualizacja: {_service.UpdateDescription}\nInstancja: {_existing!.Instance}\nPodmiot: {_existing.Database}\nSerwer: {_existing.Server}\nKonto: {_existing.Account}\n\nsettings.json, klucz API, reguła zapory i zadanie pozostaną bez zmian.\nWymagana jest przerwa w działaniu adaptera. Nie wykonujemy automatycznego rollbacku.\nStary pilot bez endpointów maintenance może wymagać ręcznego zamknięcia przed aktualizacją.";
                ShowPage(4); return;
            }
            if (_removing)
            {
                if (MessageBox.Show(this, $"Odinstalować {_existing!.Instance} z podmiotu {_existing.Database}? Konfiguracja i logi zostaną zachowane.", "Odinstalowanie", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                ShowPage(5); await _service.UninstallAsync(false, false, ct);
                ResultText.Text = "Odinstalowano instancję. Zachowano chronioną konfigurację i logi oraz współdzielony pakiet na serwerze.";
                return;
            }
            DatabaseText.Text = $"{_service.State!.Server} → {_service.State.Database}\nWersja adaptera: {_service.State.AdapterVersion}";
            ShowPage(3);
        }
        finally { SqlPasswordBox.Clear(); }
    });
    private async void Credentials_Click(object sender, RoutedEventArgs e) => await Execute(ct =>
    {
        var credentials = WindowsCredentials.Prompt(this, "Podaj konto Windows, na którym InsLauncher ma zapamiętany właściwy podmiot. Wymagane jest hasło, nie PIN.");
        try
        {
            if (_existing is not null && credentials.Sid != _existing.AccountSid) throw new InvalidOperationException("Wybierz konto zapisane dla tej instancji.");
            AccountText.Text = credentials.Account; _credentials?.Dispose(); _credentials = credentials;
        }
        catch { credentials.Dispose(); throw; }
        return Task.CompletedTask;
    });
    private async void Summary_Click(object sender, RoutedEventArgs e) => await Execute(ct =>
    {
        var request = Request(); request.Validate(HasSettings());
        if (_credentials is null && _existing is null) throw new InvalidOperationException("Wybierz konto Windows.");
        _service!.UpdateRequest(request);
        SummaryText.Text = $"Instancja: {request.Instance}\nPodmiot: {_service.State!.Database}\nSerwer: {request.Server}\nKonto: {AccountText.Text}\nPort: {request.Port}\nDostęp: {request.RemoteAddresses} ({request.Profiles})\nSeq: {(request.SeqEnabled ? request.SeqUrl : "pominięty — logi lokalne")}\nSkrót: {(request.DesktopShortcut ? "tak" : "pominięty")}\nTest po restarcie: {(RebootCheck.IsChecked == true ? "zapytaj po instalacji" : "pominięty")}";
        ShowPage(4); return Task.CompletedTask;
    });
    private async void Install_Click(object sender, RoutedEventArgs e) => await Execute(async ct =>
    {
        if (_updating)
        {
            ShowPage(5); ResultText.Text = "Aktualizacja istniejącej instancji…"; AppendActivity(ResultText.Text);
            await _service!.UpdateAsync(RetryUpdateCheck.IsChecked == true, ct);
            ResultText.Text = $"Zaktualizowano do {_service.State!.AdapterVersion}. Test API, Sfery i wersji przeszedł przez istniejące zadanie Windows. Konfiguracja i klucz pozostają bez zmian.";
            return;
        }
        ShowPage(5); ResultText.Text = "Jeśli InsLauncher zapyta o podmiot, wybierz wskazany w podsumowaniu i zapamiętaj wybór."; AppendActivity("Rozpoczęto instalację / naprawę.");
        try { await _service!.InstallAsync(_credentials, ct); }
        finally { _credentials?.Dispose(); _credentials = null; NexoPasswordBox.Clear(); }
        ResultText.Text = $"Instalacja, API i pełne zadanie Windows działają.\nAdres: http://{Environment.MachineName}:{_service.State!.Port}\nKlucz TXT: {DeploymentService.Store(_service.State.Instance).DirectoryPath}\nUruchomienie przed pierwszym logowaniem można dodatkowo potwierdzić testem po restarcie.";
        if (RebootCheck.IsChecked == true && MessageBox.Show(this, "Zapisać test i uruchomić PC ponownie za 15 sekund? Zapisz wcześniej otwarte dokumenty.", "Test po restarcie", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            new PostRebootTestCoordinator().Prepare(_service.State.Instance, _service.State.Account);
            PostRebootTestCoordinator.RestartComputer();
        }
    });
    private void RefreshProgress()
    {
        StepsText.Text = string.Join("\n", _service?.State?.Steps.Select(s => $"{s.Status switch { StepStatus.Completed => "✓", StepStatus.Failed => "✗", StepStatus.Skipped => "—", StepStatus.Running => "…", _ => "○" }}  {s.Title}") ?? []);
    }
    private async void Check_Click(object sender, RoutedEventArgs e) => await Execute(async ct =>
    {
        var state = Selected(); await ResetService(); _service!.UseState(state); ShowPage(5); AppendActivity($"Sprawdzanie instancji {state.Instance}…");
        await _service.TestAsync(ct); ResultText.Text = "Health, API key i Sfera działają. Diagnostyka nie zmieniła instalacji.";
    });
    private async void RetryTest_Click(object sender, RoutedEventArgs e) => await Execute(async ct =>
    {
        await _service!.RetryVerificationAsync(ct);
        ResultText.Text = _service.State?.Status == OperationStatus.Ready
            ? "Weryfikacja API i autostartu zakończona poprawnie. Nie instalowano pakietu ponownie."
            : "Test API przeszedł. Pozostałe etapy nadal wymagają naprawy.";
    });
    private async void ExportKey_Click(object sender, RoutedEventArgs e) => await Execute(ct =>
    {
        var state = _service?.State ?? throw new InvalidOperationException("Wybierz instancję.");
        var settings = new InstanceSettingsWriter().ReadExisting(state.Instance) ?? throw new InvalidOperationException("Brak klucza.");
        var dialog = new SaveFileDialog { FileName = DeploymentNames.GetApiKeyFileName(state.Instance), Filter = "Klucz API (*.txt)|*.txt" };
        if (dialog.ShowDialog(this) == true)
        {
            SafePath.EnsureNoReparsePoints(dialog.FileName);
            if (MessageBox.Show(this, "Plik będzie zawierał jawny klucz API. Zapisz go w miejscu dostępnym tylko dla uprawnionych osób. Kontynuować?", "Eksport klucza", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                File.Copy(Path.Combine(DeploymentService.Store(state.Instance).DirectoryPath, DeploymentNames.GetApiKeyFileName(state.Instance)), dialog.FileName, true);
        }
        return Task.CompletedTask;
    });
    private async void Diagnostics_Click(object sender, RoutedEventArgs e) => await Execute(ct =>
    {
        var dialog = new SaveFileDialog { FileName = "connector-diagnostics.json", Filter = "Raport (*.json)|*.json" };
        if (dialog.ShowDialog(this) == true)
            DeploymentStore.AtomicWrite(dialog.FileName, JsonSerializer.Serialize(_service?.State, new JsonSerializerOptions { WriteIndented = true }));
        return Task.CompletedTask;
    });
    private async void Home_Click(object sender, RoutedEventArgs e) => await Execute(async ct => { await ResetService(); ShowPage(0); RefreshInstances(); });
    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var target = _page switch
        {
            1 => 0,
            2 => _existing is not null && !_removing ? 1 : 0,
            3 => 2,
            4 when _updating => 2,
            4 => 3,
            _ => Math.Max(0, _page - 1)
        };
        ShowPage(target);
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) { _operation?.Cancel(); ShowError("Anulowanie nastąpi po zakończeniu bieżącej operacji Nexo."); }
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_busy) { e.Cancel = true; ShowError("Najpierw anuluj operację i poczekaj na bezpieczne zakończenie etapu."); return; }
        _credentials?.Dispose();
    }
    private async void RestorePackage_Click(object sender, RoutedEventArgs e) => await Execute(ct =>
    {
        var state = Selected();
        var dialog = new OpenFileDialog { Filter = "Oryginalny pakiet (*.mpkg)|*.mpkg" };
        if (dialog.ShowDialog(this) != true) return Task.CompletedTask;
        if (DeploymentService.Hash(dialog.FileName) != state.PackageSha256) throw new InvalidDataException("To nie jest pakiet zapisany dla tej instancji (inna suma SHA-256).");
        var target = SafePath.Child(DeploymentService.Store(state.Instance).DirectoryPath, state.PackagePath);
        if (!Path.GetFullPath(dialog.FileName).Equals(target, StringComparison.OrdinalIgnoreCase)) File.Copy(dialog.FileName, target, true);
        ShowError("Przywrócono oryginalny pakiet. Możesz rozpocząć naprawę.");
        return Task.CompletedTask;
    });
    private async void RestoreSettings_Click(object sender, RoutedEventArgs e) => await Execute(ct =>
    {
        var state = Selected();
        var path = InstanceSettingsWriter.GetSettingsPath(state.Instance);
        SafePath.EnsureNoReparsePoints(path + ".backup");
        if (!File.Exists(path + ".backup")) throw new InvalidOperationException("Brak kopii konfiguracji.");
        var contents = File.ReadAllText(path + ".backup");
        var parsed = JsonNode.Parse(contents) ?? throw new InvalidDataException("Kopia konfiguracji jest uszkodzona.");
        if (parsed["Observability"]?["AdapterInstance"]?.GetValue<string>() != state.Instance) throw new InvalidDataException("Kopia dotyczy innej instancji.");
        if (MessageBox.Show(this, "Przywrócić poprzednią konfigurację? Obecny plik zostanie zachowany jako settings.json.damaged. Uruchom następnie naprawę.", "Odzyskanie konfiguracji", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            if (File.Exists(path)) DeploymentStore.AtomicWrite(path + ".damaged", File.ReadAllText(path));
            DeploymentStore.AtomicWrite(path, contents);
            ShowError("Odzyskano konfigurację. Uruchom naprawę, aby uzgodnić zasoby Windows.");
        }
        return Task.CompletedTask;
    });
    private async void LocalRemove_Click(object sender, RoutedEventArgs e) => await Execute(async ct =>
    {
        var state = _existing ?? throw new InvalidOperationException("Wybierz instancję.");
        if (MessageBox.Show(this, $"Usunąć lokalne zasoby {state.Instance}? Pakiet pozostanie przypisany na serwerze. Dokończ odłączenie po odzyskaniu połączenia.", "Usunięcie częściowe", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await ResetService(); _service!.UseState(state); ShowPage(5);
        await _service.UninstallAsync(true, false, ct);
        ResultText.Text = "Usunięto lokalnie. Odłączenie pakietu na serwerze oczekuje na dokończenie; konfiguracja została zachowana.";
    });
    private async void Purge_Click(object sender, RoutedEventArgs e) => await Execute(ct =>
    {
        var state = _service?.State ?? throw new InvalidOperationException("Wybierz instancję.");
        if (state.Status != OperationStatus.Removed) throw new InvalidOperationException("Dane można usunąć dopiero po pełnym odinstalowaniu instancji.");
        if (MessageBox.Show(this, $"Trwale usunąć konfigurację, klucz API i logi instancji {state.Instance}? Zewnętrzne kopie klucza nie zostaną usunięte.", "Usunięcie danych", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            SafePath.DeleteTree(DeploymentService.InstancesRoot, Path.GetDirectoryName(InstanceSettingsWriter.GetSettingsPath(state.Instance))!);
            ShowPage(0); RefreshInstances();
        }
        return Task.CompletedTask;
    });
    private async void Import_Click(object sender, RoutedEventArgs e) => await Execute(ct =>
    {
        _updating = false; RetryUpdateCheck.Visibility = Visibility.Collapsed; ExecuteButton.Content = "Zainstaluj / napraw";
        var selected = SelectedItem();
        if (!selected.CanImport) throw new InvalidOperationException("Wybrana pozycja nie jest starszą instalacją do przejęcia.");
        var dialog = new OpenFileDialog { Filter = "Oryginalny pakiet pilota (*.mpkg)|*.mpkg" };
        if (dialog.ShowDialog(this) != true) return Task.CompletedTask;
        var manifest = PilotImporter.Inspect(dialog.FileName);
        if (!manifest.Instance.Equals(selected.Instance, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("MPKG dotyczy innej instancji niż wybrana na liście.");
        var settings = new InstanceSettingsWriter().ReadExisting(manifest.Instance) ?? throw new InvalidOperationException("Nie znaleziono lokalnej konfiguracji tego pilota.");
        var root = JsonNode.Parse(File.ReadAllText(InstanceSettingsWriter.GetSettingsPath(manifest.Instance)))!;
        _existing = null; _removing = false; _importPackage = dialog.FileName;
        InstanceBox.Text = manifest.Instance; InstanceBox.IsReadOnly = true; ServerBox.IsReadOnly = false;
        PortBox.Text = settings.Port.ToString(); NexoUserBox.Text = root["Nexo"]?["SystemLogin"]?["NexoUser"]?.GetValue<string>() ?? "";
        SqlWindowsCheck.IsChecked = false; SqlUserBox.Text = "sa"; SqlPasswordBox.Clear(); NexoPasswordBox.Clear(); ShowPage(2);
        SubtitleText.Text = "Potwierdź serwer i podmiot oryginalnego MPKG. Przejęcie zachowa jego dotychczasową tożsamość.";
        return Task.CompletedTask;
    });
}
