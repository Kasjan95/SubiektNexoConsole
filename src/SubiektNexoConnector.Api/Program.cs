using SubiektNexoConnector.Api.Auth;
using SubiektNexoConnector.Api.ErrorHandling;
using SubiektNexoConnector.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    var apiAuthenticationOptions = builder.Services.AddApiAuthentication(
        builder.Configuration,
        builder.Environment);

    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails(options =>
    {
        options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        };
    });

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.AddApiAuthenticationSwagger(apiAuthenticationOptions);
    });

    builder.Services.AddNexoInfrastructure(
        builder.Configuration,
        NexoConnectionModeResolver.UseConfig(args));

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseStatusCodePages(async statusCodeContext =>
    {
        var httpContext = statusCodeContext.HttpContext;
        var response = httpContext.Response;

        await Results.Problem(statusCode: response.StatusCode).ExecuteAsync(httpContext);
    });

    app.UseSerilogRequestLogging();

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