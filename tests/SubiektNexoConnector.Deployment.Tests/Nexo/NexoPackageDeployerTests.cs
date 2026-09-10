using SubiektNexoConnector.Deployment.Nexo;

namespace SubiektNexoConnector.Deployment.Tests.Nexo;

public sealed class NexoPackageDeployerTests
{
    [Fact]
    public void ParseDatabase_ReadsQuickInstallerDatabaseFormat()
    {
        var database = NexoPackageDeployer.ParseDatabase("Nexo_Pilot;False;True;61.0.1.1234");

        Assert.NotNull(database);
        Assert.Equal("Nexo_Pilot", database.Name);
        Assert.False(database.HasPackage);
        Assert.True(database.CanDeploy);
        Assert.Equal(new Version(61, 0, 1, 1234), database.Version);
    }

    [Fact]
    public void ParseDatabase_RejectsUnknownFormat()
    {
        Assert.Null(NexoPackageDeployer.ParseDatabase("Nexo_Pilot;True"));
    }

    [Fact]
    public void SqlConnectionParameters_AllowsEmptyPasswordForSqlAuthentication()
    {
        var result = SqlConnectionParameters.Create(false, " sa ", "");

        Assert.False(result.WindowsAuthentication);
        Assert.Equal("sa", result.User);
        Assert.Equal("", result.Password);
    }

    [Fact]
    public void SqlConnectionParameters_RequiresSqlUserButNotPassword()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            SqlConnectionParameters.Create(false, " ", null));

        Assert.Contains("login SQL", error.Message);
    }

    [Fact]
    public void SqlConnectionParameters_IgnoresSqlFieldsForWindowsAuthentication()
    {
        var result = SqlConnectionParameters.Create(true, "sa", "secret");

        Assert.True(result.WindowsAuthentication);
        Assert.Empty(result.User);
        Assert.Empty(result.Password);
    }
}
