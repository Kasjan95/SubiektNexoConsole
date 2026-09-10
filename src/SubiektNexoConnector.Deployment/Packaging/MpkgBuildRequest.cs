namespace SubiektNexoConnector.Deployment.Packaging;

public sealed record MpkgBuildRequest(
    string PublishDirectory,
    string OutputPath,
    string InstanceName,
    Version AdapterVersion,
    string RequiredProductVersion,
    bool OverwriteExisting = false,
    string? ExistingPackageIdentity = null);
