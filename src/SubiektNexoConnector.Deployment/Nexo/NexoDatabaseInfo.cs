namespace SubiektNexoConnector.Deployment.Nexo;

public sealed record NexoDatabaseInfo(
    string Name,
    bool HasPackage,
    bool HasRequiredPackages,
    Version Version)
{
    public bool CanDeploy => HasRequiredPackages;

    public string Description => HasPackage
        ? $"{Name} — Nexo {Version} — pakiet zostanie zaktualizowany"
        : $"{Name} — Nexo {Version}";
}
