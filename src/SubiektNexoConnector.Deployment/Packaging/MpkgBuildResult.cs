namespace SubiektNexoConnector.Deployment.Packaging;

public sealed record MpkgBuildResult(
    string OutputPath,
    string PackageIdentity,
    string LaunchScenarioName,
    int FileCount);
