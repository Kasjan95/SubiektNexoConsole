using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SubiektNexoConnector.Api.Auth;

public static class ApiAuthenticationExtensions
{
    public static ApiAuthenticationOptions AddApiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var apiAuthenticationOptions = configuration
            .GetSection(ApiAuthenticationOptions.SectionName)
            .Get<ApiAuthenticationOptions>() ?? new ApiAuthenticationOptions();

        var apiKeyFromEnvironment = configuration[ApiAuthenticationOptions.ApiKeyEnvironmentVariableName];

        if (!string.IsNullOrWhiteSpace(apiKeyFromEnvironment))
            apiAuthenticationOptions.ApiKey = apiKeyFromEnvironment;

        apiAuthenticationOptions.Validate(environment);

        services.AddSingleton(apiAuthenticationOptions);
        services.AddAuthorization();

        if (apiAuthenticationOptions.Mode == ApiAuthenticationMode.ApiKey)
        {
            services
                .AddAuthentication(ApiKeyAuthenticationDefaults.AuthenticationScheme)
                .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                    ApiKeyAuthenticationDefaults.AuthenticationScheme,
                    _ => { });
        }

        return apiAuthenticationOptions;
    }

    public static IApplicationBuilder UseApiAuthentication(
        this IApplicationBuilder app,
        ApiAuthenticationOptions apiAuthenticationOptions)
    {
        if (apiAuthenticationOptions.Mode == ApiAuthenticationMode.ApiKey)
            app.UseAuthentication();

        app.UseAuthorization();

        return app;
    }

    public static ControllerActionEndpointConventionBuilder RequireApiAuthentication(
        this ControllerActionEndpointConventionBuilder endpoints,
        ApiAuthenticationOptions apiAuthenticationOptions)
    {
        if (apiAuthenticationOptions.Mode == ApiAuthenticationMode.ApiKey)
            endpoints.RequireAuthorization();

        return endpoints;
    }

    public static void AddApiAuthenticationSwagger(
        this SwaggerGenOptions swaggerOptions,
        ApiAuthenticationOptions apiAuthenticationOptions)
    {
        if (apiAuthenticationOptions.Mode != ApiAuthenticationMode.ApiKey)
            return;

        swaggerOptions.AddSecurityDefinition(
            ApiKeyAuthenticationDefaults.AuthenticationScheme,
            new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                Name = apiAuthenticationOptions.HeaderName,
                In = ParameterLocation.Header,
                Description = $"Pass the API key in the {apiAuthenticationOptions.HeaderName} header."
            });

        swaggerOptions.AddSecurityRequirement(
            new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = ApiKeyAuthenticationDefaults.AuthenticationScheme
                        }
                    },
                    Array.Empty<string>()
                }
            });
    }
}
