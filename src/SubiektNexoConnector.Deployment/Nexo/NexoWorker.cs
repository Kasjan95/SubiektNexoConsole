using System.Windows;
using System.Windows.Threading;

namespace SubiektNexoConnector.Deployment.Nexo;

// PackageDeployer owns WPF/COM objects: keep its lifetime on one STA dispatcher.
public sealed class NexoWorker : IAsyncDisposable
{
    private readonly TaskCompletionSource<Dispatcher> _dispatcher = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private NexoPackageDeployer? _deployer;
    public NexoWorker(string launcher)
    {
        var thread = new Thread(() =>
        {
            try
            {
                _deployer = new NexoPackageDeployer(new Window(), launcher);
                _dispatcher.SetResult(Dispatcher.CurrentDispatcher);
                Dispatcher.Run();
            }
            catch (Exception e) { _dispatcher.TrySetException(e); }
            finally { _deployer?.Dispose(); }
        })
        { IsBackground = true, Name = "Nexo deployment STA" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }
    public async Task<T> Invoke<T>(Func<NexoPackageDeployer, T> action)
    {
        var dispatcher = await _dispatcher.Task;
        return await dispatcher.InvokeAsync(() => action(_deployer!));
    }
    public async ValueTask DisposeAsync()
    {
        if (_dispatcher.Task.IsFaulted) return;
        var dispatcher = await _dispatcher.Task;
        dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
    }
}
