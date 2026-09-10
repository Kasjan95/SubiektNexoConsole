using System.Security.Principal;

namespace SubiektNexoConnector.Deployment.Windows;

public static class WindowsAccount
{
    public static string GetCurrentName() => WindowsIdentity.GetCurrent().Name;
}
