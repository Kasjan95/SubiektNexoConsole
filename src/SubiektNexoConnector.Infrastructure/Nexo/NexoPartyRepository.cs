using InsERT.Moria.Sfera;
using SubiektNexoConnector.Core.Application.Parties.Shared;
using SubiektNexoConnector.Infrastructure.Abstractions;

namespace SubiektNexoConnector.Infrastructure.Nexo
{
    public class NexoPartyRepository : IPartyRepository
    {
        private readonly ISessionFactory _sessionFactory;
        public NexoPartyRepository(ISessionFactory sessionFactory)
        {
            _sessionFactory = sessionFactory;
        }
        public IReadOnlyCollection<PartyBasicDto> GetAll()
        {
            using Uchwyt sfera = _sessionFactory.Create();
            return sfera.
                Podmioty().
                Dane.
                Wszystkie().
                ToList().
                Select(
                    a => new PartyBasicDto(
                        a.Sygnatura.PelnaSygnatura,
                        a.NazwaSkrocona,
                        a.Typ,
                        a.Podtyp,
                        PartyCodeDictionary.GetTypeName(a.Typ),
                        PartyCodeDictionary.GetSubtypeName(a.Typ, a.Podtyp),
                        (int)a.StatusKlienta,
                        PartyCodeDictionary.GetCustomerStatusName((int)a.StatusKlienta),
                        a.NIP,
                        a.Aktywny,
                        a.Osoba?.Imie,
                        a.Osoba?.Nazwisko,
                        a.Firma?.Nazwa
                        )
                ).ToList();
        }
    }
}
