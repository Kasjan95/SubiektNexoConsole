using InsERT.Moria.Sfera;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SubiektNexoConnector.Core.Application.Products;
using SubiektNexoConnector.Core.Application.Warehouses;
using SubiektNexoConnector.Infrastructure.Abstractions;
using SubiektNexoConnector.Infrastructure.Configuration;
using SubiektNexoConnector.Infrastructure.Nexo;

namespace SubiektNexoConnector.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNexoInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool useConfig)
    {
        var appConfig = AppConfigBinder.Bind(configuration);

        services.AddSingleton(appConfig);

        services.AddSingleton<DanePolaczenia>(_ =>
        {
            if (useConfig)
            {
                Console.WriteLine("Using development database connection settings from configuration.");
                return DanePolaczenia.Jawne(
                    appConfig.Database.SqlServer,
                    appConfig.Database.DatabaseName,
                    appConfig.Database.UseSqlAuth,
                    appConfig.Database.SqlUser,
                    appConfig.Database.SqlPassword);
            }

            return DanePolaczenia.Odbierz();
        });

        services.AddSingleton<ISessionFactory>(sp =>
            new NexoSessionFactory(
                sp.GetRequiredService<AppConfig>(),
                sp.GetRequiredService<DanePolaczenia>()));

        services.AddTransient<IProductRepository, NexoProductRepository>();
        services.AddTransient<IWarehouseRepository, NexoWarehouseRepository>();

        services.AddTransient<GetProductsHandler>();
        services.AddTransient<GetProductDetailsHandler>();
        services.AddTransient<GetWarehousesHandler>();

        return services;
    }
}