using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SubiektNexoConnector.Core.Application.Warehouses;
using SubiektNexoConnector.Infrastructure;
using System;

internal class Program
{
    static void Main(string[] args)
    {
        try
        {
            var builder = Host.CreateApplicationBuilder(args);
            var useConfig = NexoConnectionModeResolver.UseConfig(args);

            builder.Services.AddNexoInfrastructure(
                builder.Configuration,
                useConfig);

            using var host = builder.Build();

            Console.WriteLine("Configuration loaded successfully.");

            var handler = host.Services.GetRequiredService<GetWarehousesHandler>();
            var warehouses = handler.Handle(new GetWarehousesQuery());

            foreach (var warehouse in warehouses)
            {
                Console.WriteLine($"{warehouse.Symbol} | {warehouse.Name}");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Application startup failed:");
            Console.WriteLine(ex.Message);
            Console.ResetColor();
            Environment.Exit(1);
        }
    }
}
