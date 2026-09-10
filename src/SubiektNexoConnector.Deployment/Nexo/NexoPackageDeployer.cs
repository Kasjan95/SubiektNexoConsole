using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows;

namespace SubiektNexoConnector.Deployment.Nexo;

/// <summary>
/// Adapter for PackageDeployer.dll embedded by InsERT's QuickInstaller template.
/// The reflection boundary deliberately mirrors PackageDeployerAdapter from that template.
/// </summary>
public sealed class NexoPackageDeployer : IDisposable
{
    private readonly string _nexoInstallationPath;
    private readonly object _deployer;
    private readonly Type _deployerType;
    private readonly ResolveEventHandler _assemblyResolveHandler;
    private readonly Func<AssemblyLoadContext, AssemblyName, Assembly?> _resolvingHandler;
    private readonly INotifyPropertyChanged _statusSource;

    public NexoPackageDeployer(Window owner, string insLauncherPath)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var launcherPath = Path.GetFullPath(insLauncherPath);
        if (!File.Exists(launcherPath))
            throw new FileNotFoundException("Nie znaleziono InsLauncher.exe.", launcherPath);
        Windows.TrustedLauncher.Validate(launcherPath);

        _nexoInstallationPath = Path.GetDirectoryName(launcherPath)
            ?? throw new InvalidOperationException("Nie można ustalić katalogu instalacji Nexo.");
        _assemblyResolveHandler = ResolveAssembly;
        _resolvingHandler = ResolveAssembly;
        AppDomain.CurrentDomain.AssemblyResolve += _assemblyResolveHandler;
        AssemblyLoadContext.Default.Resolving += _resolvingHandler;

        try
        {
            var packageDeployerAssembly = Assembly.Load(new EmbeddedPackageDeployer().ReadAllBytes());
            _deployerType = packageDeployerAssembly.GetType("PackageDeployer.Deployer", throwOnError: true)!;
            var constructor = _deployerType.GetConstructor([typeof(Window), typeof(string)])
                ?? throw new InvalidOperationException("Nieobsługiwana wersja komponentu PackageDeployer.");
            _deployer = constructor.Invoke([owner, "Nexo"]);
            _statusSource = (INotifyPropertyChanged)_deployer;
            _statusSource.PropertyChanged += DeployerOnPropertyChanged;
        }
        catch
        {
            AppDomain.CurrentDomain.AssemblyResolve -= _assemblyResolveHandler;
            AssemblyLoadContext.Default.Resolving -= _resolvingHandler;
            throw;
        }
    }

    public event EventHandler<string>? StatusChanged;

    public (string Package, string[] Scenarios) GetPackageInfo(string packagePath)
    {
        var result = (Tuple<string, string[]>)InvokeStatic("GetPackageInfo", Path.GetFullPath(packagePath));
        return (result.Item1, result.Item2);
    }

    public IReadOnlyList<NexoDatabaseInfo> GetDatabases(string packagePath)
    {
        var raw = (IEnumerable<string>?)Invoke("TryGetDatabaseInfos", Path.GetFullPath(packagePath));
        return raw?.Select(ParseDatabase).Where(database => database is not null).Cast<NexoDatabaseInfo>().ToArray()
            ?? [];
    }

    public bool Deploy(string packagePath, string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException("Nie wybrano podmiotu Nexo.");
        return (bool)Invoke("Deploy", Path.GetFullPath(packagePath), new[] { databaseName });
    }

    // Exact signature from InsERT's QuickInstallerTemplate/src/PackageDeployerAdapter.cs.
    // Leave the server package in place: other databases / clients may still depend on it.
    public bool Undeploy(string packagePath, string databaseName) =>
        (bool)Invoke("Undeploy", Path.GetFullPath(packagePath), new[] { databaseName }, false);

    public void SetServer(string server, bool windowsAuthentication, string user, string password)
    {
        try
        {
            _deployerType.InvokeMember("SetServerConnectionParameters", BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod,
                null, _deployer, [server, windowsAuthentication, user, password]);
        }
        catch (TargetInvocationException error) when (error.InnerException is not null) { throw CreateReadableException(error.InnerException); }
    }

    public void Dispose()
    {
        _statusSource.PropertyChanged -= DeployerOnPropertyChanged;
        AppDomain.CurrentDomain.AssemblyResolve -= _assemblyResolveHandler;
        AssemblyLoadContext.Default.Resolving -= _resolvingHandler;
    }

    public static NexoDatabaseInfo? ParseDatabase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var parts = value.Split(';');
        if (parts.Length != 4
            || string.IsNullOrWhiteSpace(parts[0])
            || !bool.TryParse(parts[1], out var hasPackage)
            || !bool.TryParse(parts[2], out var hasRequiredPackages)
            || !Version.TryParse(parts[3], out var version))
        {
            return null;
        }

        return new NexoDatabaseInfo(parts[0], hasPackage, hasRequiredPackages, version);
    }

    private object Invoke(string methodName, params object[] arguments)
    {
        try
        {
            return _deployerType.InvokeMember(
                methodName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod,
                binder: null,
                _deployer,
                arguments)
                ?? throw new InvalidOperationException($"Komponent Nexo nie zwrócił wyniku operacji {methodName}.");
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw CreateReadableException(exception.InnerException);
        }
    }

    private object InvokeStatic(string methodName, params object[] arguments)
    {
        try
        {
            return _deployerType.InvokeMember(
                methodName,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod,
                binder: null,
                target: null,
                arguments)
                ?? throw new InvalidOperationException($"Komponent Nexo nie zwrócił wyniku operacji {methodName}.");
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw CreateReadableException(exception.InnerException);
        }
    }

    private static InvalidOperationException CreateReadableException(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(current.Message) && !messages.Contains(current.Message))
                messages.Add(current.Message);
        }
        return new InvalidOperationException(string.Join(Environment.NewLine, messages), exception);
    }

    private void DeployerOnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName != "Status")
            return;
        var status = _deployerType.GetProperty("Status")?.GetValue(_deployer) as string;
        if (!string.IsNullOrWhiteSpace(status))
            StatusChanged?.Invoke(this, status);
    }

    private Assembly? ResolveAssembly(object? sender, ResolveEventArgs args) =>
        ResolveAssembly(AssemblyLoadContext.Default, new AssemblyName(args.Name));

    private Assembly? ResolveAssembly(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        if (assemblyName.Name is null || assemblyName.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
            return null;
        var path = Path.Combine(_nexoInstallationPath, assemblyName.Name + ".dll");
        if (!File.Exists(path))
            return null;
        return context.LoadFromAssemblyPath(path);
    }
}
