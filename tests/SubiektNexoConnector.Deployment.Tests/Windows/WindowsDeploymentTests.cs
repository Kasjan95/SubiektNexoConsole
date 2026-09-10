using SubiektNexoConnector.Deployment.Windows;
using SubiektNexoConnector.Deployment.Testing;
using System.Net;

namespace SubiektNexoConnector.Deployment.Tests.Windows;

public sealed class WindowsDeploymentTests
{
    [Fact]
    public void PortCheck_DetectsListenerOnSelectedPort()
    {
        var listeners = new[]
        {
            new IPEndPoint(IPAddress.Loopback, 15001),
            new IPEndPoint(IPAddress.Any, 15002)
        };

        Assert.True(PortAvailabilityChecker.IsInUse(listeners, 15001));
        Assert.False(PortAvailabilityChecker.IsInUse(listeners, 15003));
    }

    [Fact]
    public void FirewallRuleName_IsScopedToInstance()
    {
        Assert.Equal(
            "SubiektNexoConnector - laptop-pilot",
            FirewallRuleInstaller.GetRuleName("laptop-pilot"));
    }

    [Fact]
    public void ShortcutArguments_UseTheDeclaredLaunchScenario()
    {
        Assert.Equal(
            "nexo/SubiektNexoConnector.laptop-pilot",
            InsLauncherShortcutInstaller.GetArguments("laptop-pilot"));
    }

    [Fact]
    public void ScheduledTaskName_IsScopedToInstance()
    {
        Assert.Equal("Adapter - laptop-pilot", AdapterScheduledTaskInstaller.GetAdapterTaskName("laptop-pilot"));
    }

    [Fact]
    public void PostRebootArguments_SelectDeclaredInstance()
    {
        Assert.Equal(
            "laptop-pilot",
            PostRebootTestCoordinator.TryGetInstance(["--post-reboot-test", "--instance", "laptop-pilot"]));
        Assert.Equal("Post-reboot test - laptop-pilot", PostRebootTestCoordinator.GetVerifierTaskName("laptop-pilot"));
        Assert.Equal("Post-reboot result - laptop-pilot", PostRebootTestCoordinator.GetViewerTaskName("laptop-pilot"));
    }

    [Fact]
    public void MissingTaskFolder_RecognizesWindowsFileNotFoundHResult()
    {
        var exception = new FileNotFoundException("The system cannot find the file specified.");

        Assert.True(AdapterScheduledTaskInstaller.IsNotFound(exception));
        Assert.True(AdapterScheduledTaskInstaller.IsNotFound(new InvalidOperationException("wrapper", exception)));
        Assert.False(AdapterScheduledTaskInstaller.IsNotFound(new UnauthorizedAccessException()));
    }
}
