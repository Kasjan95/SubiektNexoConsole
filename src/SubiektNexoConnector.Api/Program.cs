using SubiektNexoConnector.Api.Auth;
using SubiektNexoConnector.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var apiAuthenticationOptions = builder.Services.AddApiAuthentication(
    builder.Configuration,
    builder.Environment);

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
