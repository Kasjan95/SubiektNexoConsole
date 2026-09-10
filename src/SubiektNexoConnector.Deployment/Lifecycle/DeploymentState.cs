using System.Text.Json.Serialization;

namespace SubiektNexoConnector.Deployment.Lifecycle;

public enum OperationStatus { Pending, Running, Ready, NeedsRepair, Cancelled, Removed, PendingServerRemoval }
public enum StepStatus { Pending, Running, Completed, Failed, Skipped }
public sealed class StepRecord
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public StepStatus Status { get; set; }
    public string? ErrorCode { get; set; }
}

// This record is deliberately secret-free. Credentials belong only in protected settings / Windows.
public sealed class DeploymentState
{
    public int SchemaVersion { get; set; } = 1;
    public Guid OperationId { get; set; } = Guid.NewGuid();
    public string Instance { get; set; } = "";
    public string Server { get; set; } = "";
    public string Database { get; set; } = "";
    public string Account { get; set; } = "";
    public string AccountSid { get; set; } = "";
    public string LauncherPath { get; set; } = "";
    public string PackagePath { get; set; } = "";
    public string PackageSha256 { get; set; } = "";
    public string PackageIdentity { get; set; } = "";
    public string AdapterVersion { get; set; } = "";
    public string RequiredNexoVersion { get; set; } = "60,*";
    public string RemoteAddresses { get; set; } = "LocalSubnet";
    public string FirewallProfiles { get; set; } = "private,domain";
    public int Port { get; set; }
    public bool DesktopShortcut { get; set; } = true;
    public bool SeqEnabled { get; set; }
    public bool PackageInstalled { get; set; }
    public string Mode { get; set; } = "install";
    public OperationStatus Status { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<StepRecord> Steps { get; set; } = [];
    public PendingAdapterUpdate? PendingUpdate { get; set; }
    [JsonIgnore] public string Display => $"{Instance} · {Database} · :{Port} · {Status}";
}
