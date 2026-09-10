using System.Windows;
using SubiektNexoConnector.Deployment.Testing;
using SubiektNexoConnector.Deployment.Lifecycle;

namespace SubiektNexoConnector.Deployment;

public partial class App : Application
{
    private Mutex? _instanceMutex;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            try { InstallerLog.Event("fatal", args.Exception); } catch { }
            MessageBox.Show($"Instalator zakończył pracę z błędem 0x{args.Exception.HResult:X8}.\nRaport: {InstallerLog.PathName}", "Błąd instalatora");
            args.Handled = true; Shutdown(1);
        };
        try { InstallerLog.Initialize(); }
        catch (Exception error) { MessageBox.Show($"Nie można utworzyć chronionego dziennika: {error.Message}"); Shutdown(1); return; }
        var instanceName = PostRebootTestCoordinator.TryGetInstance(e.Args);
        if (e.Args.Any(argument => argument.Equals(PostRebootTestCoordinator.RunArgument, StringComparison.OrdinalIgnoreCase)))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            if (instanceName is not null)
                await new PostRebootTestCoordinator().RunHeadlessAsync(instanceName);
            Shutdown(instanceName is null ? 1 : Environment.ExitCode);
            return;
        }

        if (e.Args.Any(argument => argument.Equals(PostRebootTestCoordinator.ShowArgument, StringComparison.OrdinalIgnoreCase)))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            if (instanceName is not null)
                await new PostRebootTestCoordinator().ShowResultAsync(instanceName);
            Shutdown();
            return;
        }

        _instanceMutex = new Mutex(false, @"Global\SubiektNexoConnector.Deployment");
        bool acquired;
        try { acquired = _instanceMutex.WaitOne(0); } catch (AbandonedMutexException) { acquired = true; }
        if (!acquired) { MessageBox.Show("Inny instalator już działa na tym komputerze."); Shutdown(2); return; }
        new MainWindow().Show();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        try { _instanceMutex?.ReleaseMutex(); } catch (ApplicationException) { }
        _instanceMutex?.Dispose(); base.OnExit(e);
    }
}
