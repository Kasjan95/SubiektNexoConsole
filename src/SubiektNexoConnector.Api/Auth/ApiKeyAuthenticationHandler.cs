using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace SubiektNexoConnector.Api.Auth;

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ApiAuthenticationOptions _apiAuthenticationOptions;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ApiAuthenticationOptions apiAuthenticationOptions)
        : base(options, logger, encoder)
    {
        _apiAuthenticationOptions = apiAuthenticationOptions;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(_apiAuthenticationOptions.HeaderName, out var apiKeyHeaders))
            return Task.FromResult(AuthenticateResult.Fail("Missing API key."));

        if (apiKeyHeaders.Count != 1)
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        var providedApiKey = apiKeyHeaders[0];

        if (string.IsNullOrWhiteSpace(providedApiKey))
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        if (!ApiKeysMatch(providedApiKey, _apiAuthenticationOptions.ApiKey))
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, ApiKeyAuthenticationDefaults.PrincipalName),
            new Claim(ClaimTypes.Name, ApiKeyAuthenticationDefaults.PrincipalName)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers["WWW-Authenticate"] = $"{ApiKeyAuthenticationDefaults.AuthenticationScheme} header=\"{_apiAuthenticationOptions.HeaderName}\"";

        return Task.CompletedTask;
    }

    private static bool ApiKeysMatch(string providedApiKey, string configuredApiKey)
    {
        var providedBytes = Encoding.UTF8.GetBytes(providedApiKey);
        var configuredBytes = Encoding.UTF8.GetBytes(configuredApiKey);

        return CryptographicOperations.FixedTimeEquals(providedBytes, configuredBytes);
    }
}
