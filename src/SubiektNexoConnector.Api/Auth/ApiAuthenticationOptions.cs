using Microsoft.Extensions.Hosting;

namespace SubiektNexoConnector.Api.Auth;

public sealed class ApiAuthenticationOptions
{
    public const string SectionName = "Auth";
    public const string ApiKeyEnvironmentVariableName = "SUBIEKT_NEXO_CONNECTOR_API_KEY";

    public ApiAuthenticationMode Mode { get; set; } = ApiAuthenticationMode.ApiKey;
    public string HeaderName { get; set; } = ApiKeyAuthenticationDefaults.HeaderName;
    public string ApiKey { get; set; } = "";

    public void Validate(IHostEnvironment environment)
    {
        if (!Enum.IsDefined(Mode))
            throw new InvalidOperationException("Invalid Auth:Mode value.");

        if (string.IsNullOrWhiteSpace(HeaderName))
            throw new InvalidOperationException("Auth:HeaderName is required.");

        if (Mode == ApiAuthenticationMode.ApiKey && string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException(
                $"Auth:ApiKey is required when Auth:Mode=ApiKey. Set it in local configuration or with the {ApiKeyEnvironmentVariableName} environment variable.");
        }

        if (Mode == ApiAuthenticationMode.None && !environment.IsDevelopment())
            throw new InvalidOperationException("Auth:Mode=None is allowed only in Development.");
    }
}
