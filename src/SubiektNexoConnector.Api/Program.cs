using Serilog;
using SubiektNexoConnector.Api.Auth;
using SubiektNexoConnector.Api.Configuration;
using SubiektNexoConnector.Api.ErrorHandling;
using SubiektNexoConnector.Api.Observability;
using SubiektNexoConnector.Api.Swagger;
using SubiektNexoConnector.Infrastructure;
using SubiektNexoConnector.Infrastructure.Idempotency;
using SubiektNexoConnector.Api.Idempotency;
using SubiektNexoConnector.Infrastructure.Idempotency.Persistence;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var startupOptions = AdapterStartupOptions.Parse(args);
    using var instanceLock = startupOptions.Mode == AdapterStartupMode.InsLauncher
        ? AdapterInstanceLock.Acquire(startupOptions.InstanceName!)
        : null;
    var builder = WebApplication.CreateBuilder(startupOptions.HostArguments.ToArray());
    string? instanceSettingsPath = null;

    if (startupOptions.Mode == AdapterStartupMode.InsLauncher)
    {
        instanceSettingsPath = startupOptions.AddInstanceConfiguration(builder.Configuration);
        AdapterUrlValidator.Validate(builder.Configuration[AdapterUrlValidator.ConfigurationKey]);
    }

    var observability = builder.Configuration
        .GetSection(ObservabilityOptions.SectionName)
        .Get<ObservabilityOptions>() ?? new ObservabilityOptions();
    observability.AdapterInstance = startupOptions.ResolveAdapterInstance(observability.AdapterInstance);
    var pagination = builder.Configuration
        .GetSection(PaginationOptions.SectionName)
        .Get<PaginationOptions>() ?? new PaginationOptions();
    pagination.Validate();
    builder.Services.AddSingleton(pagination);

    builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", observability.Service)
        .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
        .Enrich.WithProperty("AdapterInstance", observability.AdapterInstance)
        .Enrich.WithProperty("NexoCompany", observability.NexoCompany)
        .Enrich.WithProperty("MachineName", Environment.MachineName));

    var apiAuthenticationOptions = builder.Services.AddApiAuthentication(
        builder.Configuration,
        builder.Environment);

    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddHealthChecks();
    builder.Services.AddProblemDetails(options =>
    {
        options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

            if (context.HttpContext.Items.TryGetValue(CorrelationIdMiddleware.HttpContextItemKey, out var correlationId))
                context.ProblemDetails.Extensions["correlationId"] = correlationId;
        };
    });

    builder.Services
        .AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        });
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.AddApiAuthenticationSwagger(apiAuthenticationOptions);
        options.SchemaFilter<OptionalSchemaFilter>();
        options.OperationFilter<IdempotencyOperationFilter>();
    });

    builder.Services.AddNexoInfrastructure(builder.Configuration, startupOptions.UseLocalNexoConnection);
    builder.Services.AddIdempotencyPersistence(builder.Configuration);

    var app = builder.Build();
    var configuredDatabasePath = builder.Configuration["Idempotency:DatabasePath"]!;
    var fullDatabasePath = System.IO.Path.GetFullPath(configuredDatabasePath, AppContext.BaseDirectory);
    var databaseLockName = "db-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(fullDatabasePath.ToUpperInvariant())));
    using var databaseLock = AdapterInstanceLock.Acquire(databaseLockName);
    var dbContextFactory = app.Services.GetRequiredService<IDbContextFactory<IdempotencyDbContext>>();

    await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
    {
        await dbContext.Database.MigrateAsync();
    }
    await app.Services.GetRequiredService<IdempotencyOperationStore>().RecoverInterruptedAsync();
    var runtimeApiAuthenticationOptions = app.Services.GetRequiredService<ApiAuthenticationOptions>();

    Log.Information(
        "Adapter starting in {StartupMode} mode for instance {AdapterInstance}. InstanceSettingsPath: {InstanceSettingsPath}",
        startupOptions.Mode,
        startupOptions.InstanceName,
        instanceSettingsPath);

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();
    app.UseStatusCodePages(async statusCodeContext =>
    {
        var httpContext = statusCodeContext.HttpContext;
        var response = httpContext.Response;

        await Results.Problem(statusCode: response.StatusCode).ExecuteAsync(httpContext);
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "SubiektNexoConnector.Api v1");
            c.UseRequestInterceptor(IdempotencyRequestInterceptor.Script);
            c.ConfigObject.AdditionalItems["showMutatedRequest"] = true;
        });
        app.UseHttpsRedirection();
    }

    app.UseApiAuthentication(runtimeApiAuthenticationOptions);
    app.UseMiddleware<IdempotencyMiddleware>();
    app.MapHealthChecks("/health").AllowAnonymous();
    if (runtimeApiAuthenticationOptions.Mode == ApiAuthenticationMode.ApiKey)
    {
    app.MapGet("/maintenance/info", () => Results.Ok(new
    {
        instance = startupOptions.InstanceName,
        configuredCompany = builder.Configuration["Observability:NexoCompany"],
        processId = Environment.ProcessId,
        version = typeof(Program).Assembly.GetName().Version?.ToString(),
        informationalVersion = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(Program).Assembly)?.InformationalVersion
    })).RequireApiAuthentication(runtimeApiAuthenticationOptions);
    app.MapPost("/maintenance/stop", (HttpContext context, IHostApplicationLifetime lifetime) =>
    {
        if (context.Connection.RemoteIpAddress is null || !System.Net.IPAddress.IsLoopback(context.Connection.RemoteIpAddress))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        context.Response.OnCompleted(() => { lifetime.StopApplication(); return Task.CompletedTask; });
        return Results.Accepted();
    }).RequireApiAuthentication(runtimeApiAuthenticationOptions);
    }
    app.MapControllers().RequireApiAuthentication(runtimeApiAuthenticationOptions);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly.");
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}
public partial class Program
{
    // This is beeing used for integration testing with WebApplicationFactory<Program>
}
