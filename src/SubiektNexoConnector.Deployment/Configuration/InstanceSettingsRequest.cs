namespace SubiektNexoConnector.Deployment.Configuration;

public sealed record InstanceSettingsRequest(
    string InstanceName,
    int Port,
    string ApiKey,
    string NexoUser,
    string NexoPassword,
    string SeqUrl,
    string NexoCompany,
    bool OverwriteExisting = false);
