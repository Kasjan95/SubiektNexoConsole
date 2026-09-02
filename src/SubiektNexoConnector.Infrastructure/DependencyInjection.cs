using InsERT.Moria.Sfera;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SubiektNexoConnector.Core.Application.AdditionalFields.GetAdvancedFieldDefinitions;
using SubiektNexoConnector.Core.Application.AdditionalFields.GetBasicFieldDefinitions;
using SubiektNexoConnector.Core.Application.AdditionalFields.GetFlagDefinitions;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Parties.CreateParty;
using SubiektNexoConnector.Core.Application.Parties.GetParties;
using SubiektNexoConnector.Core.Application.Parties.GetPartyDetails;
using SubiektNexoConnector.Core.Application.Parties.PatchParty;
using SubiektNexoConnector.Core.Application.Parties.Shared;
using SubiektNexoConnector.Core.Application.Parties.Addresses.CreateAddress;
using SubiektNexoConnector.Core.Application.Parties.Addresses.PatchAddress;
using SubiektNexoConnector.Core.Application.Parties.Addresses.DeleteAddress;
using SubiektNexoConnector.Core.Application.Parties.Contacts.CreateContact;
using SubiektNexoConnector.Core.Application.Parties.Contacts.DeleteContact;
using SubiektNexoConnector.Core.Application.Parties.Contacts.PatchContact;
using SubiektNexoConnector.Core.Application.Products;
using SubiektNexoConnector.Core.Application.Warehouses;
using SubiektNexoConnector.Infrastructure.Abstractions;
using SubiektNexoConnector.Infrastructure.Configuration;
using SubiektNexoConnector.Infrastructure.Nexo;

namespace SubiektNexoConnector.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNexoInfrastructure(this IServiceCollection services, IConfiguration configuration, bool useConfig)
    {
        var appConfig = AppConfigBinder.Bind(configuration, requireDatabaseSettings: useConfig);
        services.AddSingleton(appConfig);
        services.AddSingleton(appConfig.SferaExecution);
        services.AddSingleton<DanePolaczenia>(_ =>
        {
            if (useConfig)
            {
                Console.WriteLine("Using development database connection settings from configuration.");

                return DanePolaczenia.Jawne(
                    appConfig.Database.SqlServer,
                    appConfig.Database.DatabaseName,
                    appConfig.Database.UseWindowsAuth,
                    appConfig.Database.SqlUser,
                    appConfig.Database.SqlPassword);
            }
            return DanePolaczenia.Odbierz();
        });

        services.AddSingleton<ISessionFactory>(sp => new NexoSessionFactory(sp.GetRequiredService<AppConfig>(), sp.GetRequiredService<DanePolaczenia>()));
        services.AddSingleton<ISferaExecutor, SferaExecutor>();

        services.AddTransient<IProductRepository, NexoProductRepository>();
        services.AddTransient<IWarehouseRepository, NexoWarehouseRepository>();
        services.AddTransient<IPartyRepository, NexoPartyRepository>();
        services.AddTransient<IAdditionalFieldDefinitionRepository, NexoAdditionalFieldDefinitionsRepository>();

        services.AddTransient<CreateProductHandler>();
        services.AddTransient<PatchProductHandler>();
        services.AddTransient<DeleteProductHandler>();
        services.AddTransient<GetProductsHandler>();
        services.AddTransient<GetProductDetailsHandler>();
        services.AddTransient<GetProductFromWarehouseHandler>();

        services.AddTransient<GetWarehousesHandler>();

        services.AddTransient<GetPartiesHandler>();
        services.AddTransient<GetPartyCreateOptionsHandler>();
        services.AddTransient<GetPartyDetailsHandler>();
        services.AddTransient<PatchPartyHandler>();
        services.AddTransient<CreatePartyHandler>();

        services.AddTransient<CreatePartyAddressHandler>();
        services.AddTransient<PatchPartyAddressHandler>();
        services.AddTransient<DeletePartyAddressHandler>();

        services.AddTransient<CreatePartyContactHandler>();
        services.AddTransient<DeletePartyContactHandler>();
        services.AddTransient<PatchPartyContactHandler>();

        services.AddTransient<GetAdvancedFieldDefinitionsHandler>();
        services.AddTransient<GetBasicFieldDefinitionsHandler>();
        services.AddTransient<GetFlagDefinitionHandler>();

        return services;
    }
}
