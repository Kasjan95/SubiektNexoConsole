using InsERT.Moria.Sfera;
using InsERT.Mox.Product;
using SubiektNexoConnector.Infrastructure.Abstractions;
using SubiektNexoConnector.Infrastructure.Configuration;

namespace SubiektNexoConnector.Infrastructure.Nexo;

public class NexoSessionFactory : ISessionFactory
{
    private readonly AppConfig _config;
    private readonly DanePolaczenia _danePolaczenia;

    public NexoSessionFactory(AppConfig config, DanePolaczenia danePolaczenia)
    {
        _config = config;
        _danePolaczenia = danePolaczenia;
    }

    public Uchwyt Create()
    {
        var menedzerPolaczen = new MenedzerPolaczen();

        var uchwyt = menedzerPolaczen.Polacz(
            _danePolaczenia,
            ProductId.Subiekt,
            ProductId.Subiekt);

        uchwyt.ZalogujOperatora(
            _config.SystemLogin.NexoUser,
            _config.SystemLogin.NexoPassword);

        return uchwyt;
    }
}