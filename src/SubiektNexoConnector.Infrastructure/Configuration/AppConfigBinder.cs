using Microsoft.Extensions.Configuration;

namespace SubiektNexoConnector.Infrastructure.Configuration;

public static class AppConfigBinder
{
    public static AppConfig Bind(IConfiguration configuration)
    {
        var config = configuration.GetSection("Nexo").Get<AppConfig>();

        if (config is null)
            throw new InvalidOperationException("Brak sekcji konfiguracji 'Nexo'.");

        config.Validate();
        return config;
    }
}