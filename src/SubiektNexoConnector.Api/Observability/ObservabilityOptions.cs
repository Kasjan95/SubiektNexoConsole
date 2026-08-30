namespace SubiektNexoConnector.Api.Observability;

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public string Service { get; set; } = "SubiektNexoConnector.Api";
    public string AdapterInstance { get; set; } = Environment.MachineName;
    public string NexoCompany { get; set; } = "unknown";
}
