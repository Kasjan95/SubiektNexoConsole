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
using System.Data.Entity;
using System.IO;
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
                UpdateBinaries(config.Database.DatabaseName);


                Console.WriteLine("Uruchamianie serwera...");
                NexoSessionFactory uchwytFactory = new NexoSessionFactory(config);

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
                Console.WriteLine(ex.ToString());
                Console.ResetColor();

                Environment.Exit(1);
            }
        }

        public static void UpdateBinaries(string podmiot)
        {
            var runtimeDir = Path.Combine(AppContext.BaseDirectory);

            var updater = new NexoLibraryUpdater(
                podmiot: podmiot,
                targetDirectory: runtimeDir);

            var updateResult = updater.Update();

            Console.WriteLine("=== Aktualizacja bibliotek Nexo ===");
            Console.WriteLine($"Podmiot: {updateResult.Podmiot}");
            Console.WriteLine($"Źródło Deployments: {updateResult.SourceDeploymentsPath}");
            Console.WriteLine($"Źródło Program Files: {updateResult.SourceProgramFilesPath}");
            Console.WriteLine($"Katalog runtime: {updateResult.TargetPath}");

            foreach (var file in updateResult.CopiedFiles)
                Console.WriteLine($"[COPIED]  {file}");

            foreach (var file in updateResult.SkippedFiles)
                Console.WriteLine($"[SKIPPED] {file}");

            foreach (var warning in updateResult.Warnings)
                Console.WriteLine($"[WARN]    {warning}");

            if (updateResult.HasErrors)
            {
                foreach (var error in updateResult.Errors)
                    Console.WriteLine($"[ERROR]   {error}");

                Environment.Exit(1);
                return;
            }
        }
    }
}
