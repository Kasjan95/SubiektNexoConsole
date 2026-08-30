using SubiektNexoConnector.Api.Auth;
using SubiektNexoConnector.Api.ErrorHandling;
using SubiektNexoConnector.Api.Observability;
using SubiektNexoConnector.Api.Swagger;
using SubiektNexoConnector.Infrastructure;
using Serilog;
using System.Text.Json;
using System.Text.Json.Serialization;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    var observability = builder.Configuration
        .GetSection(ObservabilityOptions.SectionName)
        .Get<ObservabilityOptions>() ?? new ObservabilityOptions();

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
    });

    builder.Services.AddNexoInfrastructure(
        builder.Configuration,
        NexoConnectionModeResolver.UseConfig(args));

    var app = builder.Build();

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
        });
    }

    app.UseHttpsRedirection();
    app.UseApiAuthentication(apiAuthenticationOptions);
    app.MapControllers().RequireApiAuthentication(apiAuthenticationOptions);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}
public partial class Program
{
    // This is beeing used for integration testing with WebApplicationFactory<Program>
}
