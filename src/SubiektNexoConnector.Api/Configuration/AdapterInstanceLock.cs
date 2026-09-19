namespace SubiektNexoConnector.Api.Configuration;

public sealed class AdapterInstanceLock : IDisposable
{
    private readonly Mutex _mutex;

    private AdapterInstanceLock(Mutex mutex)
    {
        _mutex = mutex;
    }

    public static AdapterInstanceLock Acquire(string instanceName)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
            throw new ArgumentException("Nazwa instancji nie może być pusta.", nameof(instanceName));

        var mutex = new Mutex(
            initiallyOwned: false,
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
        // The live named-object handle is the lease. It is not thread-affine,
        // so async startup/shutdown can dispose it on a different thread.
        _mutex.Dispose();
    }
}
