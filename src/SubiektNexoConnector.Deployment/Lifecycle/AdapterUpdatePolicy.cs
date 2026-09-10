using System.IO;
using SubiektNexoConnector.Deployment.Packaging;

namespace SubiektNexoConnector.Deployment.Lifecycle;

public sealed class PendingAdapterUpdate
{
    public string Version { get; set; } = "";
    public string InformationalVersion { get; set; } = "";
    public string PackagePath { get; set; } = "";
    public string PackageIdentity { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public bool Deployed { get; set; }
}

public sealed record AdapterPackageReference(string PackagePath, string Sha256, bool Required);

public static class AdapterUpdatePolicy
{
    public static string NextIdentity(string identity, Version target)
    {
        var split = identity.LastIndexOf('-');
        if (split < 1 || !Version.TryParse(identity[(split + 1)..], out var current)
            || identity[..split].Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-')))
            throw new InvalidDataException("Nieprawidłowa tożsamość oryginalnego pakietu.");
        if (target <= current) throw new InvalidOperationException("Aktualizacja wymaga nowszej wersji. Dla tej samej wersji użyj naprawy; downgrade nie jest obsługiwany.");
        return identity[..split] + "-" + DeploymentNames.NormalizeVersion(target);
    }

    public static void CommitVerified(DeploymentState state)
    {
        var update = state.PendingUpdate ?? throw new InvalidOperationException("Brak oczekującej aktualizacji.");
        if (!update.Deployed
            || !state.Steps.Any(s => s.Id == "update-smoke-test" && s.Status == StepStatus.Completed)
            || !state.Steps.Any(s => s.Id == "update-version-test" && s.Status == StepStatus.Completed))
            throw new InvalidOperationException("Nie można zatwierdzić wersji bez wdrożenia i testu.");
        state.AdapterVersion = update.Version;
        state.PackageIdentity = update.PackageIdentity;
        state.PackagePath = update.PackagePath;
        state.PackageSha256 = update.Sha256;
        state.PackageInstalled = true;
        // Clear only after the caller has persisted the committed state / maintenance registration.
    }

    public static IReadOnlyList<AdapterPackageReference> GetDetachmentPackages(DeploymentState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var result = new List<AdapterPackageReference>();
        if (state.PendingUpdate is { } pending)
        {
            var deployWasUncertain = state.Steps.Any(step => step.Id == "update-package"
                && step.Status is StepStatus.Running or StepStatus.Failed);
            if (pending.Deployed || deployWasUncertain)
                result.Add(new(pending.PackagePath, pending.Sha256, pending.Deployed));
        }

        result.Add(new(state.PackagePath, state.PackageSha256, true));
        return result.DistinctBy(package => package.PackagePath, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
