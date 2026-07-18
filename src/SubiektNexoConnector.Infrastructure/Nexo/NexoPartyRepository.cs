using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using SubiektNexoConnector.Core.Application.Parties.GetParties;
using SubiektNexoConnector.Core.Application.Parties.GetPartyDetails;
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
        #region GET
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
                    a.PobierzNazweGrupyTypu(),
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
        public PartyDetailsDto? GetDetails(GetPartyDetailsQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);
            using Uchwyt sfera = _sessionFactory.Create();
            Podmiot? party = sfera.Podmioty().Dane.Wszystkie(a => a.Sygnatura.PelnaSygnatura == query.PartySignature).FirstOrDefault();
            if (party == null)
                return null;

            List<PartyAddressDto> addresses = party.Adresy.Select(a => new PartyAddressDto(
                a.Id,
                a.Szczegoly.Ulica,
                a.Szczegoly.NrDomu,
                a.Szczegoly.NrLokalu,
                a.Szczegoly.Miejscowosc,
                a.Szczegoly.Gmina?.Nazwa,
                a.Szczegoly.Wojewodztwo?.Nazwa,
                a.Panstwo?.Nazwa,
                a.TypAdresu.Nazwa,
                a.TypAdresu.Id)).ToList();

            TradeCreditLimitDto tradeCreditLimit = new TradeCreditLimitDto(
                party.ZezwalajNaKredytKupiecki,
                party.MaksymalnyTerminPlatnosciKredytu,
                party.MaksymalnyLiczbaDniSpoznien,
                party.MaksymalnaLiczbaNiesplaconychDok,
                party.LimitKredytuKupieckiego,
                MapDocumentTradeCreditLimit(party.LimitKredytuNaSprzedazyAktywny, party.LimitKredytuNaSprzedazy),
                MapDocumentTradeCreditLimit(party.LimitKredytuNaWydaniuAktywny, party.LimitKredytuNaWydaniu),
                MapDocumentTradeCreditLimit(party.LimitKredytuNaZamowieniuAktywny, party.LimitKredytuNaZamowieniu));

            List<PartyContactDto> contacts = party.Kontakty.Select(c => new PartyContactDto(
                c.Id,
                c.Podstawowy,
                c.Rodzaj.Nazwa,
                c.Wartosc,
                c.Komentarz)).ToList();

            return new PartyDetailsDto(
                party.Sygnatura.PelnaSygnatura,
                party.NazwaSkrocona,
                party.Aktywny,
                PartyCodeDictionary.GetTypeName(party.Typ),
                party.PobierzNazweGrupyTypu(),
                party.Osoba?.Imie,
                party.Osoba?.Nazwisko,
                party.Firma?.Nazwa,
                party.NIP,
                party.NIPUE,
                party.Firma?.REGON,
                party.Firma?.KRS,
                party.Grupy.FirstOrDefault()?.Nazwa,
                party.Branze.FirstOrDefault()?.Nazwa,
                party.Cechy.Select(c => c.Nazwa).ToList(),
                party.Uwagi,
                addresses,
                contacts,
                tradeCreditLimit);
        }
        private DocumentTradeCreditLimitDto MapDocumentTradeCreditLimit(bool active, LimitKredytuKupieckiego? limit)
        {
            if (!active || limit is null)
                return new DocumentTradeCreditLimitDto(false, null, null, null);

            return new DocumentTradeCreditLimitDto(
                true,
                limit.Wartosc,
                limit.LimitPonizejWartosci,
                limit.LimitPowyzejWartosci);
        }
        #endregion
    }
}
