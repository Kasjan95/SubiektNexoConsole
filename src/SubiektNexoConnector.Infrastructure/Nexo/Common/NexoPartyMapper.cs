using InsERT.Moria.ModelDanych;
using SubiektNexoConnector.Core.Application.Parties.Addresses.Shared;
using SubiektNexoConnector.Core.Application.Parties.Contacts.Shared;
using SubiektNexoConnector.Core.Application.Parties.GetParties;
using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Infrastructure.Nexo.Common
{
    internal static class NexoPartyMapper
    {
        public static PartyBasicDto MapBasic(Podmiot party) => new(
            party.Sygnatura.PelnaSygnatura,
            party.NazwaSkrocona,
            party.Typ,
            party.Podtyp,
            PartyCodeDictionary.GetTypeName(party.Typ),
            party.PobierzNazweGrupyTypu(),
            (int)party.StatusKlienta,
            PartyCodeDictionary.GetCustomerStatusName((int)party.StatusKlienta),
            party.NIP,
            party.Aktywny,
            party.Osoba?.Imie,
            party.Osoba?.Nazwisko,
            party.Firma?.Nazwa);

        public static PartyAddressDto MapAddress(Adres address) => new(
            address.Id,
            address.Szczegoly.Ulica,
            address.Szczegoly.NrDomu,
            address.Szczegoly.NrLokalu,
            address.Szczegoly.KodPocztowy,
            address.Szczegoly.Miejscowosc,
            address.Szczegoly.Gmina?.Nazwa,
            address.Szczegoly.Wojewodztwo?.Nazwa,
            address.Panstwo?.Id,
            address.TypAdresu.Nazwa,
            address.TypAdresu.Id);

        public static PartyContactDto MapContact(Kontakt contact) => new(
            contact.Id,
            contact.Podstawowy,
            contact.Rodzaj.Id,
            contact.Rodzaj.Nazwa,
            contact.Wartosc,
            contact.Komentarz);

        public static TradeCreditLimitDto MapTradeCreditLimit(Podmiot party) => new(
            party.ZezwalajNaKredytKupiecki,
            party.MaksymalnyTerminPlatnosciKredytu,
            party.MaksymalnyLiczbaDniSpoznien,
            party.MaksymalnaLiczbaNiesplaconychDok,
            party.LimitKredytuKupieckiego,
            MapDocumentTradeCreditLimit(party.LimitKredytuNaSprzedazyAktywny, party.LimitKredytuNaSprzedazy),
            MapDocumentTradeCreditLimit(party.LimitKredytuNaWydaniuAktywny, party.LimitKredytuNaWydaniu),
            MapDocumentTradeCreditLimit(party.LimitKredytuNaZamowieniuAktywny, party.LimitKredytuNaZamowieniu));

        private static DocumentTradeCreditLimitDto MapDocumentTradeCreditLimit(
            bool active,
            LimitKredytuKupieckiego? limit)
        {
            if (!active || limit is null)
                return new DocumentTradeCreditLimitDto(false, null, null, null);

            return new DocumentTradeCreditLimitDto(
                true,
                limit.Wartosc,
                limit.LimitPonizejWartosci,
                limit.LimitPowyzejWartosci);
        }
    }
}
