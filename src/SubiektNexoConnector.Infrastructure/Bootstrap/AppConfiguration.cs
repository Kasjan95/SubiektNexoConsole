using Microsoft.Extensions.Configuration;
using SubiektNexoConnector.Infrastructure.Configuration;
using System;

namespace SubiektNexoConnector.Infrastructure.Bootstrap;

public static class AppConfiguration
{
    public static AppConfig Load()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        var config = configuration.GetSection("Nexo").Get<AppConfig>()
            ?? throw new InvalidOperationException("Nie udało się wczytać konfiguracji. Sprawdź czy plik appsettings.json istnieje i ma poprawną strukturę JSON.");

        config.Validate();

        return config;
    }
}