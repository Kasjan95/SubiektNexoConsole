namespace SubiektNexoConnector.Api.Configuration;

public static class AdapterUrlValidator
{
    public const string ConfigurationKey = "Urls";
    public const string ExampleUrl = "http://0.0.0.0:15001";

    public static void Validate(string? configuredUrls)
    {
        if (string.IsNullOrWhiteSpace(configuredUrls))
            throw InvalidUrls("The value is missing.");

        var urls = configuredUrls.Split(
            ';',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (urls.Length == 0)
            throw InvalidUrls("The value does not contain an endpoint.");

        foreach (var configuredUrl in urls)
            ValidateUrl(configuredUrl);
    }

    private static void ValidateUrl(string configuredUrl)
    {
        if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var uri))
            throw InvalidUrls($"'{configuredUrl}' is not an absolute URL.");

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            throw InvalidUrls($"'{configuredUrl}' must use HTTP or HTTPS.");

        if (string.IsNullOrWhiteSpace(uri.Host))
            throw InvalidUrls($"'{configuredUrl}' does not contain a host.");

        // A non-default port proves that the deployment selected a port explicitly.
        // The adapter does not silently fall back to Kestrel's defaults (80/443).
        if (uri.IsDefaultPort || uri.Port is < 1 or > 65535)
            throw InvalidUrls($"'{configuredUrl}' must contain an explicit non-default port.");

        if (!string.IsNullOrEmpty(uri.UserInfo))
            throw InvalidUrls($"'{configuredUrl}' cannot contain user information.");

        if (uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw InvalidUrls($"'{configuredUrl}' cannot contain a path, query string or fragment.");
    }

    private static InvalidOperationException InvalidUrls(string reason)
    {
        return new InvalidOperationException(
            $"Invalid {ConfigurationKey} configuration. {reason} " +
            $"Configure an explicit HTTP or HTTPS endpoint, for example: {ExampleUrl}");
    }
}
