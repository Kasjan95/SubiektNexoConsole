namespace SubiektNexoConnector.Deployment.Packaging;

public static class DeploymentNames
{
    public const string PackagePrefix = "SubiektNexoConnector";

    public static void ValidateInstanceName(string instanceName)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
            throw new InvalidOperationException("Nazwa instancji nie może być pusta.");

        if (instanceName.Length > 64)
            throw new InvalidOperationException("Nazwa instancji nie może przekraczać 64 znaków.");

        if (new[] { "CON", "PRN", "AUX", "NUL", "CLOCK$" }.Contains(instanceName, StringComparer.OrdinalIgnoreCase)
            || System.Text.RegularExpressions.Regex.IsMatch(instanceName, "^(COM|LPT)[1-9]$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            throw new InvalidOperationException("Ta nazwa jest zarezerwowana przez Windows.");

        if (!instanceName.All(character =>
                char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))
        {
            throw new InvalidOperationException(
                "Nazwa instancji może zawierać tylko litery ASCII, cyfry, '-' i '_'.");
        }
    }

    public static string NormalizeVersion(Version version)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (version.Major < 0 || version.Minor < 0 || version.Build < 0 || version.Revision < 0)
            throw new InvalidOperationException("Wersja adaptera musi mieć cztery nieujemne człony, np. 1.0.0.0.");

        return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }

    public static string GetPackageName(string instanceName)
    {
        ValidateInstanceName(instanceName);
        // Versioned namespace cannot collide with pilot names (which never contained '.').
        // Escape the escape character before the version delimiter; this mapping is injective.
        var encodedInstanceName = instanceName.ToLowerInvariant().Replace("_", "_u", StringComparison.Ordinal)
            .Replace("-", "_h", StringComparison.Ordinal);
        return $"{PackagePrefix}.v2.{encodedInstanceName}";
    }

    public static string GetPackageIdentity(string instanceName, Version version) =>
        $"{GetPackageName(instanceName)}-{NormalizeVersion(version)}";

    public static string GetMpkgFileName(string instanceName, Version version)
    {
        ValidateInstanceName(instanceName);
        return $"{PackagePrefix}.{instanceName}-{NormalizeVersion(version)}.mpkg";
    }

    public static string GetApiKeyFileName(string instanceName)
    {
        ValidateInstanceName(instanceName);
        return $"{PackagePrefix}.{instanceName}-api-key.txt";
    }

    public static string GetLaunchScenarioName(string instanceName)
    {
        ValidateInstanceName(instanceName);
        return $"{PackagePrefix}.{instanceName}";
    }
}
