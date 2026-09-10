using System.Net;
using System.Net.NetworkInformation;

namespace SubiektNexoConnector.Deployment.Windows;

public sealed class PortAvailabilityChecker
{
    public bool IsInUse(int port)
    {
        ValidatePort(port);
        return IsInUse(
            IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners(),
            port);
    }

    public async Task WaitUntilAvailableAsync(
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ValidatePort(port);
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (IsInUse(port) && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);

        if (IsInUse(port))
        {
            throw new InvalidOperationException(
                $"Port TCP {port} jest zajęty. Zatrzymaj działającą instancję adaptera lub wybierz inny port i uruchom instalację ponownie.");
        }
    }

    public static bool IsInUse(IEnumerable<IPEndPoint> listeners, int port)
    {
        ArgumentNullException.ThrowIfNull(listeners);
        ValidatePort(port);
        return listeners.Any(endpoint => endpoint.Port == port);
    }

    private static void ValidatePort(int port)
    {
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));
    }
}
