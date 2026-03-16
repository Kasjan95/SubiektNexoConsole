using InsERT.Moria.Sfera;
using InsERT.Mox.Product;
using SubiektNexoConsole.Application.Abstractions;
using SubiektNexoConsole.Bootstrap;
using SubiektNexoConsole.Infrastructure.Configuration;
using System;

namespace SubiektNexoConsole.Infrastructure.Nexo
{
    public class NexoSessionFactory : ISessionFactory
    {
        DanePolaczenia danePolaczenia;
        private readonly AppConfig _config;

        public NexoSessionFactory(AppConfig config, bool debug)
        {
            _config = config;
            if (debug)
            {
                Console.WriteLine("Uruchomiono w trybie debugowania. Używane będą jawne dane połączenia z konfiguracji.");
                danePolaczenia = DanePolaczenia.Jawne(
                    _config.Database.SqlServer,
                    _config.Database.DatabaseName,
                    _config.Database.UseSqlAuth,
                    _config.Database.SqlUser,
                    _config.Database.SqlPassword);
            }
            else
            {
                danePolaczenia = DanePolaczenia.Odbierz();
            }
        }

        public Uchwyt Create()
        {
            var menedzerPolaczen = new MenedzerPolaczen();

            var uchwyt = menedzerPolaczen.Polacz(danePolaczenia, ProductId.Subiekt, ProductId.Subiekt);
            uchwyt.ZalogujOperatora(
                _config.SystemLogin.NexoUser,
                _config.SystemLogin.NexoPassword);
            Console.WriteLine("Połączono z bazą Subiekt Nexo i zalogowano operatora.");
            return uchwyt;
        }
    }
}
