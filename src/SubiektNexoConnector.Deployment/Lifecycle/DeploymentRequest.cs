using SubiektNexoConnector.Deployment.Packaging;
using SubiektNexoConnector.Deployment.Windows;

namespace SubiektNexoConnector.Deployment.Lifecycle;

public sealed class DeploymentRequest
{
    public string Instance { get; init; } = "";
    public string Server { get; init; } = "";
    public string LauncherPath { get; init; } = "";
    public string? PublishDirectory { get; init; }
    public int Port { get; init; }
    public string NexoUser { get; init; } = "";
    public string NexoPassword { get; init; } = "";
    public bool SeqEnabled { get; init; }
    public string SeqUrl { get; init; } = "";
    public bool DesktopShortcut { get; init; } = true;
    public bool ReinstallPackage { get; init; }
    public string RemoteAddresses { get; init; } = "LocalSubnet";
    public string Profiles { get; init; } = "private,domain";
    public void Validate(bool existing)
    {
        var errors = GetValidationErrors(existing);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Values));
    }
    public IReadOnlyDictionary<string, string> GetValidationErrors(bool existing)
    {
        var errors = new Dictionary<string, string>();
        try { DeploymentNames.ValidateInstanceName(Instance); }
        catch (InvalidOperationException e) { errors[nameof(Instance)] = e.Message; }
        if (Port is < 1 or > 65535) errors[nameof(Port)] = "Port musi mieścić się w zakresie 1–65535.";
        if (string.IsNullOrWhiteSpace(Server) || Server.Any(char.IsControl)) errors[nameof(Server)] = "Podaj serwer SQL Nexo.";
        if (string.IsNullOrWhiteSpace(NexoUser)) errors[nameof(NexoUser)] = "Podaj operatora Nexo.";
        if (!existing && string.IsNullOrEmpty(NexoPassword)) errors[nameof(NexoPassword)] = "Podaj hasło operatora Nexo.";
        if (SeqEnabled && (!Uri.TryCreate(SeqUrl, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length > 0))
            errors[nameof(SeqUrl)] = "Włączony Seq wymaga adresu HTTP/HTTPS bez poświadczeń w URL.";
        try { FirewallRuleInstaller.Validate(RemoteAddresses, "private"); }
        catch (InvalidOperationException e) { errors[nameof(RemoteAddresses)] = e.Message; }
        try { _ = FirewallRuleInstaller.ProfileMask(Profiles); }
        catch (InvalidOperationException e) { errors[nameof(Profiles)] = e.Message; }
        return errors;
    }
}
