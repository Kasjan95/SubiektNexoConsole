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
using Microsoft.Extensions.Hosting;
using NSubstitute;
using SubiektNexoConnector.Infrastructure.Idempotency;
using SubiektNexoConnector.Infrastructure.Idempotency.Persistence;
using SubiektNexoConnector.Api.Auth;
using SubiektNexoConnector.Core.Application.Products;
using SubiektNexoConnector.Core.Application.Parties.Shared;
using SubiektNexoConnector.Core.Application.Warehouses;
using Xunit;
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.GetAdvancedFieldDefinitions;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace SubiektNexoConnector.Api.Tests.Integration;

public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    private readonly System.Collections.Concurrent.ConcurrentBag<string> _databaseDirectories = new();
    public const string ValidTestApiKey = "integration-test-api-key";

    public IProductRepository Products { get; } = Substitute.For<IProductRepository>();
    public IWarehouseRepository Warehouses { get; } = Substitute.For<IWarehouseRepository>();
    public IPartyRepository Parties { get; } = Substitute.For<IPartyRepository>();
    public IAdditionalFieldDefinitionRepository AdditionalFieldDefinitions { get; } = Substitute.For<IAdditionalFieldDefinitionRepository>();

    public TestApiFactory()
    {
        AdditionalFieldDefinitions
            .GetAdvancedFieldDefinitions(Arg.Any<GetAdvancedFieldDefinitionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new AdvancedFieldDefinitionsDto(callInfo.Arg<GetAdvancedFieldDefinitionsQuery>().Target, [], []));
    }

    public HttpClient CreateBusinessClient()
    {
        return WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IPolicyEvaluator, AllowAllPolicyEvaluator>();
            });
        }).CreateDefaultClient(new TestIdempotencyHeaderHandler());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var databaseDirectory = Path.Combine(Path.GetTempPath(), "connector-http-tests", Guid.NewGuid().ToString("N"));
        _databaseDirectories.Add(databaseDirectory);
        builder.UseEnvironment("Development");
        builder.UseSetting("Auth:Mode", "ApiKey");
        builder.UseSetting("Auth:HeaderName", ApiKeyAuthenticationDefaults.HeaderName);
        builder.UseSetting("Auth:ApiKey", ValidTestApiKey);
        builder.UseSetting("Idempotency:DatabasePath", Path.Combine(databaseDirectory, "test.db"));
        builder.UseSetting("Materialization:DatabasePath", Path.Combine(databaseDirectory, "read-model.db"));

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Mode"] = "ApiKey",
                ["Idempotency:DatabasePath"] = Path.Combine(databaseDirectory, "test.db"),
                ["Materialization:DatabasePath"] = Path.Combine(databaseDirectory, "read-model.db"),
                ["Auth:HeaderName"] = ApiKeyAuthenticationDefaults.HeaderName,
                ["Auth:ApiKey"] = ValidTestApiKey,
                ["Nexo:Database:SqlServer"] = "test-sql",
                ["Nexo:Database:DatabaseName"] = "test-db",
                ["Nexo:Database:UseWindowsAuth"] = "true",
                ["Nexo:SystemLogin:NexoUser"] = "test-user",
                ["Nexo:SystemLogin:NexoPassword"] = "test-password"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<ApiAuthenticationOptions>();
            services.RemoveAll<IProductRepository>();
            services.RemoveAll<IWarehouseRepository>();
            services.RemoveAll<IPartyRepository>();
            services.RemoveAll<IAdditionalFieldDefinitionRepository>();

            services.AddSingleton(new ApiAuthenticationOptions
            {
                Mode = ApiAuthenticationMode.ApiKey,
                HeaderName = ApiKeyAuthenticationDefaults.HeaderName,
                ApiKey = ValidTestApiKey
            });
            services.AddSingleton<IProductRepository>(provider => TestIdempotencyRepositoryProxy<IProductRepository>.Wrap(
                Products, provider.GetRequiredService<IdempotencyExecutionAccessor>(),
                provider.GetRequiredService<IdempotencyOperationStore>()));
            services.AddSingleton(Warehouses);
            services.AddSingleton<IPartyRepository>(provider => TestIdempotencyRepositoryProxy<IPartyRepository>.Wrap(
                Parties, provider.GetRequiredService<IdempotencyExecutionAccessor>(),
                provider.GetRequiredService<IdempotencyOperationStore>()));
            services.AddSingleton(AdditionalFieldDefinitions);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            CleanupDatabases();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        CleanupDatabases();
    }

    private void CleanupDatabases()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        while (_databaseDirectories.TryTake(out var directory))
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
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
