using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using SubiektNexoConnector.Api.Auth;
using SubiektNexoConnector.Core.Application.Products;
using SubiektNexoConnector.Core.Application.Warehouses;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace SubiektNexoConnector.Api.Tests.Integration;

public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    public const string ValidTestApiKey = "integration-test-api-key";

    public IProductRepository Products { get; } = Substitute.For<IProductRepository>();
    public IWarehouseRepository Warehouses { get; } = Substitute.For<IWarehouseRepository>();

    public HttpClient CreateBusinessClient()
    {
        return WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IPolicyEvaluator, AllowAllPolicyEvaluator>();
            });
        }).CreateClient();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Nexo:Database:SqlServer"] = "test-sql",
                ["Nexo:Database:DatabaseName"] = "test-db",
                ["Nexo:Database:UseWindowsAuth"] = "true",
                ["Nexo:SystemLogin:NexoUser"] = "test-user",
                ["Nexo:SystemLogin:NexoPassword"] = "test-password"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ApiAuthenticationOptions>();
            services.RemoveAll<IProductRepository>();
            services.RemoveAll<IWarehouseRepository>();

            services.AddSingleton(new ApiAuthenticationOptions
            {
                Mode = ApiAuthenticationMode.ApiKey,
                HeaderName = ApiKeyAuthenticationDefaults.HeaderName,
                ApiKey = ValidTestApiKey
            });
            services.AddSingleton(Products);
            services.AddSingleton(Warehouses);
        });
    }

    private sealed class AllowAllPolicyEvaluator : IPolicyEvaluator
    {
        public Task<AuthenticateResult> AuthenticateAsync(
            AuthorizationPolicy policy,
            HttpContext context)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "test-user"),
                new Claim(ClaimTypes.Name, "test-user")
            };

            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        public Task<PolicyAuthorizationResult> AuthorizeAsync(
            AuthorizationPolicy policy,
            AuthenticateResult authenticationResult,
            HttpContext context,
            object? resource)
        {
            return Task.FromResult(PolicyAuthorizationResult.Success());
        }
    }
}
