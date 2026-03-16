using InsERT.Moria.Asortymenty;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using InsERT.Mox.Product;
using Microsoft.Extensions.Configuration;
using SubiektNexoConsole.Application.Warehouses;
using SubiektNexoConsole.Bootstrap;
using SubiektNexoConsole.Infrastructure.Configuration;
using SubiektNexoConsole.Infrastructure.Nexo;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using Windows.UI.WebUI;

namespace SubiektNexoConsole
{
    internal class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var config = AppConfiguration.Load();
                Console.WriteLine("Konfiguracja załadowana pomyślnie.");


                NexoSessionFactory uchwytFactory = new NexoSessionFactory(config, Debugger.IsAttached);
                
                var warehouseRepository = new NexoWarehouseRepository(uchwytFactory);
                var warehouseHandler = new GetWarehousesHandler(warehouseRepository);
                var result = warehouseHandler.Handle(new GetWarehousesQuery());

                result.ToList().ForEach(w =>
                     Console.WriteLine($"Nazwa: {w.Name}, Kod: {w.Symbol}")
                 );
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Błąd startu aplikacji:");
                Console.WriteLine(ex.Message);
                Console.ResetColor();
                Environment.Exit(1);
            }
        }
    }
}
