using InsERT.Moria.Sfera;
using SubiektNexoConnector.Core.Application.Parties.GetParties;
using SubiektNexoConnector.Core.Application.Parties.Shared;
using SubiektNexoConnector.Infrastructure.Abstractions;
using SubiektNexoConnector.Infrastructure.Nexo.Common;

namespace SubiektNexoConnector.Infrastructure.Nexo
{
    public class NexoPartyRepository : IPartyRepository
    {
        private const int MaxPageSize = 1000;
        private readonly ISessionFactory _sessionFactory;

        public NexoPartyRepository(ISessionFactory sessionFactory)
        {
            _sessionFactory = sessionFactory;
        }

        public IReadOnlyCollection<PartyBasicDto> GetAll(GetPartiesQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);

            using Uchwyt sfera = _sessionFactory.Create();

            var parties = query.CustomerStatus switch
            {
                PartyCustomerStatusFilter.Standard => sfera.Podmioty().Dane.Wszystkie(a => (int)a.StatusKlienta == 0),
                PartyCustomerStatusFilter.Potential => sfera.Podmioty().Dane.Wszystkie(a => (int)a.StatusKlienta == 2),
                PartyCustomerStatusFilter.All => sfera.Podmioty().Dane.Wszystkie(),
                _ => sfera.Podmioty().Dane.Wszystkie(a => (int)a.StatusKlienta == 0)
            };

            if (query.Type.HasValue)
            {
                parties = parties.Where(a => a.Typ == query.Type.Value);
            }

            var page = Math.Max(query.Page, 1);
            var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

            var mappedParties = parties
                .OrderBy(a => a.Id)
                .ToList()
                .Select(a => new PartyBasicDto(
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
                    a.Firma?.Nazwa));

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var searchTerm = query.Search.Trim();
                mappedParties = mappedParties.Where(a =>
                    SearchTextMatcher.MatchesSearch(a, searchTerm));
            }

            return mappedParties
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }
    }
}
