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
        private readonly AppConfig _config;

        public NexoSessionFactory(AppConfig config)
        {
            _config = config;
        }

        public Uchwyt Create()
        {
            var danePolaczenia = DanePolaczenia.Jawne(
                _config.Database.SqlServer,
                "nexo_"+_config.Database.DatabaseName,
                _config.Database.UseSqlAuth,
                _config.Database.SqlUser,
                _config.Database.SqlPassword);

            var menedzerPolaczen = new MenedzerPolaczen();

            var uchwyt = menedzerPolaczen.Polacz(danePolaczenia, ProductId.Subiekt);
            uchwyt.ZalogujOperatora(
                _config.SystemLogin.NexoUser,
                _config.SystemLogin.NexoPassword);
            Console.WriteLine("Połączono z bazą Subiekt Nexo i zalogowano operatora.");
            return uchwyt;
        }
    }
}
