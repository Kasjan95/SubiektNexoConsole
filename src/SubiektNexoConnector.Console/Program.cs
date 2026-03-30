using System;
using System.Diagnostics;
using System.Linq;
using SubiektNexoConnector.Infrastructure.Nexo;
using SubiektNexoConnector.Infrastructure.Bootstrap;
using SubiektNexoConnector.Core.Application.Warehouses;
using SubiektNexoConnector.Core.Application.Products;

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

            var productRepository = new NexoProductRepository(uchwytFactory);
            var productHandler = new GetProductsHandler(productRepository);
            var products = productHandler.Handle(new GetProductsQuery());


            products.ToList().ForEach(p =>
                Console.WriteLine($"ID: {p.Id}, Nazwa: {p.Name}, Kod: {p.SKU}, EAN: {p.EAN}")
            );

            Console.WriteLine("Wyszukaj szczegóły produktu do SKU w kontekście magazynu:");
            Console.WriteLine("Magazyn:");
            String mag = Console.ReadLine();
            Console.WriteLine("SKU:");
            String sku = Console.ReadLine();

            var productDetailsHandler = new GetProductDetailsHandler(productRepository);
            var productDetails = productDetailsHandler.Handle(new GetProductDetailsQuery(sku, mag));

            if (productDetails is null)
            {
                Console.WriteLine("Nie znaleziono produktu lub magazynu.");
            }
            else
            {
                Console.WriteLine($"ID: {productDetails.Id}");
                Console.WriteLine($"SKU: {productDetails.SKU}");
                Console.WriteLine($"Name: {productDetails.Name}");
                Console.WriteLine($"EAN: {productDetails.EAN}");
                Console.WriteLine($"Warehouse: {productDetails.WarehouseSymbol}");
                Console.WriteLine($"Available: {productDetails.Available}");
                Console.WriteLine($"Reserved: {productDetails.Reserved}");
                Console.WriteLine($"Receipts: {productDetails.Receipts}");
                Console.WriteLine($"Issues: {productDetails.Issues}");
                Console.WriteLine($"Returns: {productDetails.Returns}");
            }
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
