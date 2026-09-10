namespace SubiektNexoConnector.Api.Configuration;

public sealed class AdapterInstanceLock : IDisposable
{
    private readonly Mutex _mutex;
    private bool _ownsMutex;

    private AdapterInstanceLock(Mutex mutex)
    {
        _mutex = mutex;
        _ownsMutex = true;
    }

    public static AdapterInstanceLock Acquire(string instanceName)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
            throw new ArgumentException("Nazwa instancji nie może być pusta.", nameof(instanceName));

        var mutex = new Mutex(
            initiallyOwned: true,
            $"Global\\SubiektNexoConnector-{instanceName}",
            out var createdNew);
        if (createdNew)
            return new AdapterInstanceLock(mutex);

        mutex.Dispose();
        throw new InvalidOperationException(
            $"Instancja adaptera '{instanceName}' jest już uruchomiona.");
    }

    public void Dispose()
    {
        if (_ownsMutex)
        {
            _mutex.ReleaseMutex();
            _ownsMutex = false;
        }
        _mutex.Dispose();
    }
}
