using System.IO;
using System.Runtime.InteropServices;
using SubiektNexoConnector.Deployment.Packaging;

namespace SubiektNexoConnector.Deployment.Windows;

public sealed class AdapterScheduledTaskInstaller
{
    internal const string TaskFolder = "SubiektNexoConnector";

    public ScheduledTaskResult Install(
        string instanceName,
        string launcherPath,
        string accountName,
        string accountPassword)
    {
        DeploymentNames.ValidateInstanceName(instanceName);
        if (string.IsNullOrWhiteSpace(accountName))
            throw new InvalidOperationException("Konto Windows nie może być puste.");
        if (string.IsNullOrWhiteSpace(accountPassword))
            throw new InvalidOperationException("Podaj hasło konta Windows. PIN Windows Hello nie jest hasłem konta.");

        var fullLauncherPath = Path.GetFullPath(launcherPath);
        if (!File.Exists(fullLauncherPath))
            throw new FileNotFoundException("Nie znaleziono InsLauncher.exe.", fullLauncherPath);

        var taskName = GetAdapterTaskName(instanceName);
        RegisterBootTask(
            taskName,
            fullLauncherPath,
            InsLauncherShortcutInstaller.GetArguments(instanceName),
            Path.GetDirectoryName(fullLauncherPath)!,
            accountName,
            accountPassword,
            logonType: 1,
            delay: "PT30S",
            description: $"Uruchamia adapter {instanceName} przez InsLauncher przy starcie systemu.");

        return new ScheduledTaskResult($"\\{TaskFolder}\\{taskName}", accountName);
    }

    public static string GetAdapterTaskName(string instanceName)
    {
        DeploymentNames.ValidateInstanceName(instanceName);
        return $"Adapter - {instanceName}";
    }

    internal static void RegisterBootTask(
        string taskName,
        string executablePath,
        string arguments,
        string workingDirectory,
        string accountName,
        string? accountPassword,
        int logonType,
        string delay,
        string description,
        bool enabled = true)
    {
        dynamic? service = null;
        dynamic? root = null;
        dynamic? folder = null;
        dynamic? definition = null;
        dynamic? trigger = null;
        dynamic? action = null;
        dynamic? registered = null;
        try
        {
            service = CreateService();
            service.Connect();
            root = service.GetFolder("\\");
            folder = GetOrCreateFolder(service, root);
            definition = service.NewTask(0);
            definition.RegistrationInfo.Description = description;
            definition.Principal.UserId = accountName;
            definition.Principal.LogonType = logonType;
            definition.Principal.RunLevel = 1;
            definition.Settings.Enabled = enabled;
            definition.Settings.StartWhenAvailable = true;
            definition.Settings.DisallowStartIfOnBatteries = false;
            definition.Settings.StopIfGoingOnBatteries = false;
            definition.Settings.MultipleInstances = 2;
            definition.Settings.RestartCount = 3;
            definition.Settings.RestartInterval = "PT1M";
            definition.Settings.ExecutionTimeLimit = "PT0S";

            trigger = definition.Triggers.Create(8);
            trigger.Delay = delay;
            trigger.Enabled = true;

            action = definition.Actions.Create(0);
            action.Path = executablePath;
            action.Arguments = arguments;
            action.WorkingDirectory = workingDirectory;

            registered = folder.RegisterTaskDefinition(
                taskName,
                definition,
                6,
                accountName,
                accountPassword,
                logonType,
                null);
        }
        catch (COMException exception)
        {
            throw new InvalidOperationException(
                $"Nie udało się utworzyć zadania Harmonogramu (0x{exception.HResult:X8}). Sprawdź hasło i prawo logowania wsadowego.",
                exception);
        }
        finally
        {
            Release(registered, action, trigger, definition, folder, root, service);
        }
    }

    internal static void RegisterLogonTask(
        string taskName,
        string executablePath,
        string arguments,
        string accountName,
        string description)
    {
        dynamic? service = null;
        dynamic? root = null;
        dynamic? folder = null;
        dynamic? definition = null;
        dynamic? trigger = null;
        dynamic? action = null;
        dynamic? registered = null;
        try
        {
            service = CreateService();
            service.Connect();
            root = service.GetFolder("\\");
            folder = GetOrCreateFolder(service, root);
            definition = service.NewTask(0);
            definition.RegistrationInfo.Description = description;
            definition.Principal.UserId = accountName;
            definition.Principal.LogonType = 3;
            definition.Principal.RunLevel = 1;
            definition.Settings.Enabled = true;
            definition.Settings.StartWhenAvailable = true;
            definition.Settings.ExecutionTimeLimit = "PT10M";

            trigger = definition.Triggers.Create(9);
            trigger.UserId = accountName;
            trigger.Enabled = true;

            action = definition.Actions.Create(0);
            action.Path = executablePath;
            action.Arguments = arguments;
            action.WorkingDirectory = Path.GetDirectoryName(executablePath)!;

            registered = folder.RegisterTaskDefinition(taskName, definition, 6, accountName, null, 3, null);
        }
        finally
        {
            Release(registered, action, trigger, definition, folder, root, service);
        }
    }

    public static void DeleteTask(string taskName)
    {
        dynamic? service = null;
        dynamic? folder = null;
        try
        {
            service = CreateService();
            service.Connect();
            folder = service.GetFolder($"\\{TaskFolder}");
            folder.DeleteTask(taskName, 0);
        }
        catch (Exception exception) when (IsNotFound(exception))
        {
            // The one-shot task may already be gone; deletion is best effort.
        }
        finally
        {
            Release(folder, service);
        }
    }

    public static void StopAdapterTask(string instanceName)
    {
        DeploymentNames.ValidateInstanceName(instanceName);
        dynamic? service = null;
        dynamic? folder = null;
        dynamic? task = null;
        try
        {
            service = CreateService();
            service.Connect();
            folder = service.GetFolder($"\\{TaskFolder}");
            task = folder.GetTask(GetAdapterTaskName(instanceName));
            task.Stop(0);
        }
        catch (Exception exception) when (IsNotFound(exception))
        {
            // Pierwsza instalacja albo zadanie zostało wcześniej usunięte.
        }
        finally
        {
            Release(task, folder, service);
        }
    }

    public static void ValidateCredentials(string instance, string launcher, WindowsCredentials credentials)
    {
        var name = "Credential check - " + Guid.NewGuid().ToString("N");
        try
        {
            credentials.Use(password =>
            {
                RegisterBootTask(name, launcher, InsLauncherShortcutInstaller.GetArguments(instance), Path.GetDirectoryName(launcher)!,
                    credentials.Account, password, 1, "PT30S", "SubiektNexoConnector credential verification", enabled: false);
                return true;
            });
        }
        finally { DeleteTask(name); }
    }

    public static bool Matches(string instance, string launcher, string sid, bool requireEnabled = true, bool requirePassword = true)
    {
        dynamic? service = null, folder = null, task = null, definition = null, actions = null, action = null, principal = null;
        try
        {
            service = CreateService(); service.Connect(); folder = service.GetFolder($"\\{TaskFolder}");
            task = folder.GetTask(GetAdapterTaskName(instance)); definition = task.Definition;
            actions = definition.Actions; if (actions.Count != 1) return false;
            action = actions.Item(1); principal = definition.Principal;
            var user = (string)principal.UserId;
            var actualSid = user.StartsWith("S-1-", StringComparison.Ordinal) ? user :
                ((System.Security.Principal.SecurityIdentifier)new System.Security.Principal.NTAccount(user).Translate(typeof(System.Security.Principal.SecurityIdentifier))).Value;
            return (!requireEnabled || task.Enabled) && (!requirePassword || principal.LogonType == 1) && actualSid == sid
                && string.Equals((string)action.Path, launcher, StringComparison.OrdinalIgnoreCase)
                && action.Arguments == InsLauncherShortcutInstaller.GetArguments(instance);
        }
        catch (Exception e) when (IsNotFound(e)) { return false; }
        finally { Release(principal, action, actions, definition, task, folder, service); }
    }

    public static void StartAdapterTask(string instance)
    {
        dynamic? service = null, folder = null, task = null, running = null;
        try
        {
            service = CreateService(); service.Connect(); folder = service.GetFolder($"\\{TaskFolder}");
            task = folder.GetTask(GetAdapterTaskName(instance)); running = task.Run(null);
        }
        finally { Release(running, task, folder, service); }
    }

    public static bool Exists(string instance)
    {
        dynamic? service = null, folder = null, task = null;
        try
        {
            service = CreateService(); service.Connect(); folder = service.GetFolder($"\\{TaskFolder}");
            task = folder.GetTask(GetAdapterTaskName(instance)); return true;
        }
        catch (Exception e) when (IsNotFound(e)) { return false; }
        finally { Release(task, folder, service); }
    }
    private static object CreateService()
    {
        var type = Type.GetTypeFromProgID("Schedule.Service")
            ?? throw new InvalidOperationException("Usługa Harmonogramu zadań jest niedostępna.");
        return Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("Nie udało się połączyć z Harmonogramem zadań.");
    }

    private static object GetOrCreateFolder(dynamic service, dynamic root)
    {
        try
        {
            return service.GetFolder($"\\{TaskFolder}");
        }
        catch (Exception exception) when (IsNotFound(exception))
        {
            return root.CreateFolder(TaskFolder, null);
        }
    }

    public static bool IsNotFound(Exception exception)
    {
        const int FileNotFoundHResult = unchecked((int)0x80070002);
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.HResult == FileNotFoundHResult)
                return true;
        }
        return false;
    }

    private static void Release(params object?[] values)
    {
        foreach (var value in values)
        {
            if (value is not null && Marshal.IsComObject(value))
                Marshal.FinalReleaseComObject(value);
        }
    }
}
