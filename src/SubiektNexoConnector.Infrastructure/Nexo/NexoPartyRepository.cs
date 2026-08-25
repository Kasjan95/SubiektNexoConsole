using InsERT.Moria.ModelDanych;
using InsERT.Moria.Klienci;
using InsERT.Moria.Flagi;
using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.PolaWlasne2;
using InsERT.Moria.Sfera;
using SubiektNexoConnector.Core.Application.Common;
using SubiektNexoConnector.Core.Application.Parties.CreateParty;
using SubiektNexoConnector.Core.Application.Parties.GetParties;
using SubiektNexoConnector.Core.Application.Parties.GetPartyDetails;
using SubiektNexoConnector.Core.Application.Parties.PatchParty;
using SubiektNexoConnector.Core.Application.Parties.Shared;
using SubiektNexoConnector.Infrastructure.Abstractions;
using SubiektNexoConnector.Infrastructure.Nexo.Common;
using SubiektNexoConnector.Core.Application.Parties.Addresses.Shared;
using SubiektNexoConnector.Core.Application.Parties.Addresses.CreateAddress;
using SubiektNexoConnector.Core.Application.Parties.Addresses.PatchAddress;
using SubiektNexoConnector.Core.Application.Parties.Addresses.DeleteAddress;
using SubiektNexoConnector.Core.Application.Parties.Contacts.Shared;
using SubiektNexoConnector.Core.Application.Parties.Contacts.CreateContact;
using SubiektNexoConnector.Core.Application.Parties.Contacts.DeleteContact;
using SubiektNexoConnector.Core.Application.Parties.Contacts.PatchContact;

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
        public IReadOnlyCollection<PartyBasicDto> GetAllParties(GetPartiesQuery query)
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
                .Select(NexoPartyMapper.MapBasic);

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

        public PartyCreateOptionsDto GetCreateOptions()
        {
            using Uchwyt sfera = _sessionFactory.Create();

            var partyTypes = Enum.GetValues<PodtypPodmiotu>()
                .Select(subtype => new PartyTypeOptionDto(
                    PodtypPodmiotuZakresy.OkreslaOsobe(subtype) ? (short)1 : (short)2,
                    (byte)subtype,
                    subtype.ToString()))
                .ToList();

            return new PartyCreateOptionsDto(
                partyTypes,
                sfera.TypyAdresu().Dane.Wszystkie()
                    .ToList()
                    .OrderBy(addressType => addressType.Nazwa)
                    .Select(addressType => new ReferenceDataOptionDto(addressType.Id, addressType.Nazwa))
                    .ToList(),
                sfera.RodzajeKontaktu().Dane.Wszystkie()
                    .ToList()
                    .OrderBy(contactType => contactType.Nazwa)
                    .Select(contactType => new ReferenceDataOptionDto(contactType.Id, contactType.Nazwa))
                    .ToList(),
                sfera.Panstwa().Dane.Wszystkie()
                    .ToList()
                    .OrderBy(country => country.Nazwa)
                    .Select(country => new CountryOptionDto(
                        country.Id,
                        country.Nazwa,
                        country.KodISOAlfa2()))
                    .ToList(),
                sfera.Grupy().Dane.Wszystkie()
                    .ToList()
                    .OrderBy(group => group.Nazwa)
                    .Select(group => new ReferenceDataOptionDto(group.Id, group.Nazwa))
                    .ToList(),
                sfera.Branze().Dane.Wszystkie()
                    .ToList()
                    .OrderBy(industry => industry.Nazwa)
                    .Select(industry => new ReferenceDataOptionDto(industry.Id, industry.Nazwa))
                    .ToList(),
                sfera.Cechy().Dane.Wszystkie()
                    .ToList()
                    .OrderBy(feature => feature.Nazwa)
                    .Select(feature => new ReferenceDataOptionDto(feature.Id, feature.Nazwa))
                    .ToList());
        }

        public PartyDetailsDto? GetDetailsParty(GetPartyDetailsQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);
            using Uchwyt sfera = _sessionFactory.Create();
            Podmiot? party = sfera.Podmioty().Dane.Wszystkie(a => a.Sygnatura.PelnaSygnatura == query.PartySignature).FirstOrDefault();
            if (party == null)
                return null;

            var partyBo = sfera.Podmioty().Znajdz(party);
            if (partyBo is null)
                return null;

            var basicFieldDefinitions = sfera.PodajObiektTypu<IProstePolaWlasne>();
            var basicFieldValues = sfera.UtworzPolaWlasneStdAccessor(partyBo.Dane);

            var advancedFields = sfera.PodajObiektTypu<IZaawansowanePolaWlasne>();
            var hasAdvancedFields = advancedFields.SprobujPobracZaawansowanePolaWlasne(
                typeof(Podmiot),
                out var advancedFieldDefinitions);
            var advancedFieldValues = hasAdvancedFields
                ? sfera.UtworzPolaWlasneAdv2Accessor(
                    partyBo.Dane,
                    PolaWlasneAdv2AccessorFactoryNullHandlingKind.CreateReadonlyStub)
                : null;

            List<PartyAddressDto> addresses = party.Adresy.Select(NexoPartyMapper.MapAddress).ToList();
            TradeCreditLimitDto tradeCreditLimit = NexoPartyMapper.MapTradeCreditLimit(party);
            List<PartyContactDto> contacts = party.Kontakty.Select(NexoPartyMapper.MapContact).ToList();

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
                party.Branze.Select(industry => industry.Nazwa).ToList(),
                party.Cechy.Select(c => c.Nazwa).ToList(),
                party.Uwagi,
                party.FlagaWlasna is null
                    ? null
                    : new FlagAssignmentDto(
                        party.FlagaWlasna.Id,
                        party.FlagHeader?.Description),
                NexoAdditionalFieldValueMapper.MapBasic(
                    basicFieldDefinitions,
                    basicFieldValues,
                    typeof(Podmiot)),
                advancedFieldValues is null
                    ? []
                    : NexoAdditionalFieldValueMapper.MapAdvanced(
                        advancedFieldDefinitions,
                        advancedFieldValues),
                addresses,
                contacts,
                tradeCreditLimit);
        }
        #endregion
        #region PATCH
        public string? PatchParty(PatchPartyCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);

            using Uchwyt sfera = _sessionFactory.Create();
            using var partyBo = sfera.Podmioty().Znajdz(command.PartySignature);

            if (partyBo is null)
                return null;

            if (IsFlagOnlyPatch(command))
            {
                UpdatePartyFlag(sfera, partyBo.Dane.Id, command.Flag.Value);
                return partyBo.Dane.Sygnatura.PelnaSygnatura;
            }

            var isLocked = partyBo.Zablokuj();
            if (!isLocked)
                throw new InvalidOperationException("Party could not be locked for update.");

            string updatedSignature;

            try
            {
                var party = partyBo.Dane;

                if (command.Signature.HasValue)
                    party.Sygnatura.PelnaSygnatura = command.Signature.Value!;

                if (command.DisplayName.HasValue)
                    party.NazwaSkrocona = command.DisplayName.Value!;

                if (command.IsActive.HasValue)
                    party.Aktywny = command.IsActive.Value;

                UpdatePerson(party, command);
                UpdateCompany(party, command);

                if (command.TaxId.HasValue)
                    party.NIP = command.TaxId.Value;

                if (command.EuTaxId.HasValue)
                {
                    party.NIPUE = command.EuTaxId.Value;
                    party.PodatnikUE = command.EuTaxId.Value is not null;
                }

                if (command.Notes.HasValue)
                    party.Uwagi = command.Notes.Value;

                if (command.PartyGroupId.HasValue)
                    SetPartyGroup(sfera, party, command.PartyGroupId.Value);

                if (command.IndustryIds.HasValue)
                    SetIndustries(sfera, party, command.IndustryIds.Value!);

                if (command.FeatureIds.HasValue)
                    SetFeatures(sfera, party, command.FeatureIds.Value!);

                if (command.BasicFields.HasValue)
                {
                    var basicFields = sfera.PodajObiektTypu<IProstePolaWlasne>();
                    var basicValues = sfera.UtworzPolaWlasneStdAccessor(party);

                    NexoAdditionalFieldValueMapper.ApplyBasic(
                        basicFields,
                        basicValues,
                        typeof(Podmiot),
                        command.BasicFields.Value!);
                }

                if (command.AdvancedFields.HasValue)
                {
                    var advancedFields = sfera.PodajObiektTypu<IZaawansowanePolaWlasne>();
                    if (!advancedFields.SprobujPobracZaawansowanePolaWlasne(
                        typeof(Podmiot),
                        out var advancedDefinitions))
                    {
                        throw new InvalidOperationException("Party does not support advanced fields.");
                    }

                    var advancedValues = sfera.UtworzPolaWlasneAdv2Accessor(
                        party,
                        PolaWlasneAdv2AccessorFactoryNullHandlingKind.CreateAndAttach);

                    NexoAdditionalFieldValueMapper.ApplyAdvanced(
                        advancedDefinitions,
                        advancedValues,
                        command.AdvancedFields.Value!);
                }

                if (!partyBo.Zapisz())
                {
                    throw new InvalidOperationException(NexoValidationMessageBuilder.Build(
                        "Party update failed:",
                        partyBo.PobierzKomunikatyBledow()));
                }

                updatedSignature = party.Sygnatura.PelnaSygnatura;
            }
            finally
            {
                if (isLocked)
                    partyBo.Odblokuj();
            }

            if (command.Flag.HasValue)
                UpdatePartyFlag(sfera, partyBo.Dane.Id, command.Flag.Value);

            return updatedSignature;
        }
        public PartyAddressDto? PatchPartyAddress(PatchPartyAddressCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);
            using Uchwyt sfera = _sessionFactory.Create();
            using var partyBo = sfera.Podmioty().Znajdz(command.PartySignature);
            if (partyBo is null)
                return null;
            var party = partyBo.Dane;
            var address = party.Adresy.SingleOrDefault(item => item.Id == command.AddressId);
            if (address is null)
                return null;

            if (!partyBo.Zablokuj())
                throw new InvalidOperationException("Party could not be locked for address update.");

            try
            {
                if (command.Street.HasValue)
                    address.Szczegoly.Ulica = command.Street.Value ?? string.Empty;
                if (command.HouseNumber.HasValue)
                    address.Szczegoly.NrDomu = command.HouseNumber.Value ?? string.Empty;
                if (command.ApartmentNumber.HasValue)
                    address.Szczegoly.NrLokalu = command.ApartmentNumber.Value ?? string.Empty;
                if (command.PostalCode.HasValue)
                    address.Szczegoly.KodPocztowy = command.PostalCode.Value ?? string.Empty;
                if (command.City.HasValue)
                    address.Szczegoly.Miejscowosc = command.City.Value ?? string.Empty;
                if (command.CountryId.HasValue)
                {
                    if (command.CountryId.Value is null)
                    {
                        address.Panstwo = null;
                    }
                    else
                    {
                        var country = sfera.Panstwa().Dane.Pierwszy(c => c.Id == command.CountryId.Value.Value)
                            ?? throw new InvalidOperationException($"Country with ID '{command.CountryId.Value}' was not found.");
                        address.Panstwo = country;
                    }
                }
                if (!partyBo.Zapisz())
                {
                    throw new InvalidOperationException(NexoValidationMessageBuilder.Build(
                        "Party address update failed:",
                        partyBo.PobierzKomunikatyBledow()));
                }
                return NexoPartyMapper.MapAddress(address);
            }
            finally
            {
                partyBo.Odblokuj();
            }
        }
        public PartyContactDto? PatchPartyContact(PatchPartyContactCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);
            using Uchwyt sfera = _sessionFactory.Create();
            using var partyBo = sfera.Podmioty().Znajdz(command.PartySignature);
            if (partyBo is null)
                return null;

            var contact = partyBo.Dane.Kontakty.SingleOrDefault(item => item.Id == command.ContactId);
            if (contact is null)
                return null;

            if (!partyBo.Zablokuj())
                throw new InvalidOperationException("Party could not be locked for contact update.");

            try
            {
                if (command.ContactValue.HasValue)
                    contact.Wartosc = command.ContactValue.Value ?? string.Empty;
                if (command.IsPrimary.HasValue)
                    contact.Podstawowy = command.IsPrimary.Value;
                if (command.ContactDescription.HasValue)
                    contact.Komentarz = command.ContactDescription.Value ?? string.Empty;
                if (!partyBo.Zapisz())
                {
                    throw new InvalidOperationException(NexoValidationMessageBuilder.Build(
                        "Party contact update failed:",
                        partyBo.PobierzKomunikatyBledow()));
                }
                return NexoPartyMapper.MapContact(contact);
            }
            finally
            {
                partyBo.Odblokuj();
            }
        }
        private static bool IsFlagOnlyPatch(PatchPartyCommand command) =>
            command.Flag.HasValue
            && !command.Signature.HasValue
            && !command.DisplayName.HasValue
            && !command.IsActive.HasValue
            && !command.FirstName.HasValue
            && !command.LastName.HasValue
            && !command.CompanyName.HasValue
            && !command.TaxId.HasValue
            && !command.EuTaxId.HasValue
            && !command.BusinessRegistryNumber.HasValue
            && !command.NationalCourtRegisterNumber.HasValue
            && !command.PartyGroupId.HasValue
            && !command.IndustryIds.HasValue
            && !command.FeatureIds.HasValue
            && !command.Notes.HasValue
            && !command.BasicFields.HasValue
            && !command.AdvancedFields.HasValue;

        private static void UpdatePartyFlag(
            Uchwyt sfera,
            int partyId,
            FlagAssignmentDto? flag)
        {
            var flags = sfera.PodajObiektTypu<IFlagiWlasne>();
            var result = flag is null
                ? flags.UsunFlage(typeof(Podmiot), partyId)
                : flags.NadajFlage(flag.Id, flag.Comment, typeof(Podmiot), partyId);

            if (!result)
                throw new InvalidOperationException(
                    $"Party flag update failed for party id {partyId} and flag id {flag?.Id}: {result}");
        }

        private static void UpdatePerson(Podmiot party, PatchPartyCommand command)
        {
            if (!command.FirstName.HasValue && !command.LastName.HasValue)
                return;

            var person = party.Osoba
                ?? throw new InvalidOperationException("First name and last name can only be updated for a person.");

            if (command.FirstName.HasValue)
                person.Imie = command.FirstName.Value;

            if (command.LastName.HasValue)
                person.Nazwisko = command.LastName.Value;
        }

        private static void UpdateCompany(Podmiot party, PatchPartyCommand command)
        {
            if (!command.CompanyName.HasValue &&
                !command.BusinessRegistryNumber.HasValue &&
                !command.NationalCourtRegisterNumber.HasValue)
            {
                return;
            }

            var company = party.Firma
                ?? throw new InvalidOperationException("Company data can only be updated for an organization.");

            if (command.CompanyName.HasValue)
                company.Nazwa = command.CompanyName.Value;

            if (command.BusinessRegistryNumber.HasValue)
                company.REGON = command.BusinessRegistryNumber.Value;

            if (command.NationalCourtRegisterNumber.HasValue)
                company.KRS = command.NationalCourtRegisterNumber.Value;
        }

        #endregion
        #region POST
        public PartyDetailsDto CreateParty(CreatePartyCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);
            using Uchwyt sfera = _sessionFactory.Create();
            using var partyBo = command.Type switch
            {
                1 => sfera.Podmioty().UtworzOsobe(),
                2 => sfera.Podmioty().UtworzFirme(),
                _ => throw new InvalidOperationException($"Party type '{command.Type}' is not supported.")
            };

            partyBo.AutoSymbol();
            partyBo.Zablokuj();
            var party = partyBo.Dane;

            if (command.Signature is not null)
                party.Sygnatura.PelnaSygnatura = command.Signature;

            party.NazwaSkrocona = command.DisplayName;
            party.Podtyp = command.Subtype;
            party.Aktywny = true;
            if (command.FirstName is not null || command.LastName is not null)
            {
                var person = party.Osoba ?? throw new InvalidOperationException("Person data can only be set for a person.");
                person.Imie = command.FirstName;
                person.Nazwisko = command.LastName;
            }
            if (command.CompanyName is not null ||
                command.BusinessRegistryNumber is not null ||
                command.NationalCourtRegisterNumber is not null)
            {
                var company = party.Firma ?? throw new InvalidOperationException("Company data can only be set for an organization.");

                if (command.CompanyName is not null)
                    company.Nazwa = command.CompanyName;

                if (command.BusinessRegistryNumber is not null)
                    company.REGON = command.BusinessRegistryNumber;

                if (command.NationalCourtRegisterNumber is not null)
                    company.KRS = command.NationalCourtRegisterNumber;
            }
            if (command.TaxId is not null)
                party.NIP = command.TaxId;

            if (command.EuTaxId is not null)
            {
                party.NIPUE = command.EuTaxId;
                party.PodatnikUE = true;
            }
            party.Uwagi = command.Notes;
            SetPartyGroup(sfera, party, command.PartyGroupId);
            SetIndustries(sfera, party, command.IndustryIds);
            SetFeatures(sfera, party, command.FeatureIds);
            AddAddresses(sfera, partyBo, party, command.Addresses);
            AddContacts(sfera, party, command.Contacts);
            if (!partyBo.Zapisz())
            {
                throw new InvalidOperationException(NexoValidationMessageBuilder.Build(
                    "Party creation failed:",
                    partyBo.PobierzKomunikatyBledow()));
            }
            partyBo.Odblokuj();
            return GetDetailsParty(new GetPartyDetailsQuery(party.Sygnatura.PelnaSygnatura))!;
        }
        public CreatePartyAddressResult? CreatePartyAddress(CreatePartyAddressCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);
            using Uchwyt sfera = _sessionFactory.Create();
            using var partyBo = sfera.Podmioty().Znajdz(command.PartySignature);
            if (partyBo is null)
                return null;
            var party = partyBo.Dane;

            if (!partyBo.Zablokuj())
                throw new InvalidOperationException("Party could not be locked for address creation.");

            try
            {
                var primaryAddressTypeId = sfera.TypyAdresu().DaneDomyslne.Glowny.Id;
                var isCreated = command.Address.AddressTypeId != primaryAddressTypeId || party.AdresPodstawowy is null;
                var address = AddAddresses(sfera, partyBo, party, [command.Address]).Single();
                if (!partyBo.Zapisz())
                {
                    throw new InvalidOperationException(NexoValidationMessageBuilder.Build(
                        "Party address creation failed:",
                        partyBo.PobierzKomunikatyBledow()));
                }
                return new CreatePartyAddressResult(NexoPartyMapper.MapAddress(address), isCreated);
            }
            finally
            {
                partyBo.Odblokuj();
            }
        }
        public PartyContactDto? CreatePartyContact(CreatePartyContactCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);
            using Uchwyt sfera = _sessionFactory.Create();
            using var partyBo = sfera.Podmioty().Znajdz(command.PartySignature);
            if (partyBo is null)
                return null;
            var party = partyBo.Dane;

            if (!partyBo.Zablokuj())
                throw new InvalidOperationException("Party could not be locked for contact creation.");

            try
            {
                AddContacts(sfera, party, [command.Contact]);
                if (!partyBo.Zapisz())
                {
                    throw new InvalidOperationException(NexoValidationMessageBuilder.Build(
                        "Party contact creation failed:",
                        partyBo.PobierzKomunikatyBledow()));
                }
                return NexoPartyMapper.MapContact(party.Kontakty.Last());
            }
            finally
            {
                partyBo.Odblokuj();
            }
        }

        private static void SetPartyGroup(Uchwyt sfera, Podmiot party, int? partyGroupId)
        {
            party.Grupy.Clear();

            if (partyGroupId is null)
                return;

            var partyGroup = sfera.Grupy().Dane.Pierwszy(group => group.Id == partyGroupId.Value)
                ?? throw new InvalidOperationException($"Party group '{partyGroupId}' was not found.");

            party.Grupy.Add(partyGroup);
        }

        private static void SetIndustries(Uchwyt sfera, Podmiot party, IReadOnlyCollection<int> industryIds)
        {
            var ids = industryIds.Distinct().ToHashSet();
            var industries = sfera.Branze().Dane.Wszystkie(industry => ids.Contains(industry.Id)).ToList();

            if (industries.Count != ids.Count)
                throw new InvalidOperationException("One or more industry IDs were not found.");

            party.Branze.Clear();

            foreach (var industry in industries)
                party.Branze.Add(industry);
        }

        private static void SetFeatures(Uchwyt sfera, Podmiot party, IReadOnlyCollection<int> featureIds)
        {
            var ids = featureIds.Distinct().ToHashSet();
            var features = sfera.Cechy().Dane.Wszystkie(feature => ids.Contains(feature.Id)).ToList();

            if (features.Count != ids.Count)
                throw new InvalidOperationException("One or more feature IDs were not found.");

            party.Cechy.Clear();

            foreach (var feature in features)
                party.Cechy.Add(feature);
        }

        private static IReadOnlyCollection<Adres> AddAddresses(Uchwyt sfera, IPodmiot partyBo, Podmiot party, IReadOnlyCollection<PartyAddressInput> addresses)
        {
            var addressTypes = sfera.TypyAdresu();
            var addedAddresses = new List<Adres>();

            foreach (var command in addresses)
            {
                var addressType = addressTypes.Dane.Pierwszy(type => type.Id == command.AddressTypeId)
                    ?? throw new InvalidOperationException($"Address type '{command.AddressTypeId}' was not found.");
                var address = addressType.Id == addressTypes.DaneDomyslne.Glowny.Id
                    ? party.AdresPodstawowy ?? partyBo.DodajAdres(addressType)
                    : partyBo.DodajAdres(addressType);

                address.Szczegoly ??= new AdresSzczegoly();
                address.Szczegoly.Ulica = command.Street ?? string.Empty;
                address.Szczegoly.NrDomu = command.HouseNumber ?? string.Empty;
                address.Szczegoly.NrLokalu = command.ApartmentNumber ?? string.Empty;
                address.Szczegoly.KodPocztowy = command.PostalCode ?? string.Empty;
                address.Szczegoly.Miejscowosc = command.City ?? string.Empty;

                if (command.CountryId is not null)
                {
                    address.Panstwo = sfera.Panstwa().Dane.Pierwszy(country => country.Id == command.CountryId.Value)
                        ?? throw new InvalidOperationException($"Country '{command.CountryId}' was not found.");
                }

                addedAddresses.Add(address);
            }

            return addedAddresses;
        }

        private static void AddContacts(Uchwyt sfera, Podmiot party, IReadOnlyCollection<PartyContactInput> contacts)
        {
            foreach (var command in contacts)
            {
                var contactType = sfera.RodzajeKontaktu().Dane.Pierwszy(type =>
                    type.Id == command.ContactTypeId)
                    ?? throw new InvalidOperationException($"Contact type '{command.ContactTypeId}' was not found.");
                var contact = new Kontakt();
                party.Kontakty.Add(contact);
                contact.Rodzaj = contactType;
                contact.Wartosc = command.Value ?? string.Empty;
                contact.Podstawowy = command.IsPrimary;
                contact.Komentarz = command.Comment ?? string.Empty;
            }
        }
        #endregion
        #region DELETE
        public DeletePartyResourceResult DeletePartyAddress(DeletePartyAddressCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);
            using Uchwyt sfera = _sessionFactory.Create();
            using var partyBo = sfera.Podmioty().Znajdz(command.PartySignature);
            if (partyBo is null)
                return DeletePartyResourceResult.NotFound;

            var address = partyBo.Dane.Adresy.SingleOrDefault(item => item.Id == command.AddressId);
            if (address == null)
                return DeletePartyResourceResult.NotFound;

            if (!partyBo.Zablokuj())
            {
                throw new InvalidOperationException("Party could not be locked for address deletion.");
            }

            try
            {
                partyBo.UsunAdres(address);
                if (!partyBo.Zapisz())
                {
                    throw new InvalidOperationException(NexoValidationMessageBuilder.Build(
                        "Party address deletion failed:",
                        partyBo.PobierzKomunikatyBledow()));
                }
                return DeletePartyResourceResult.Deleted;
            }
            finally
            {
                partyBo.Odblokuj();
            }
        }
        public DeletePartyResourceResult DeletePartyContact(DeletePartyContactCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);
            using Uchwyt sfera = _sessionFactory.Create();
            using var partyBo = sfera.Podmioty().Znajdz(command.PartySignature);
            if (partyBo is null)
                return DeletePartyResourceResult.NotFound;

            var contact = partyBo.Dane.Kontakty.SingleOrDefault(item => item.Id == command.ContactId);
            if (contact == null)
                return DeletePartyResourceResult.NotFound;

            if (!partyBo.Zablokuj())
            {
                throw new InvalidOperationException("Party could not be locked for contact deletion.");
            }

            try
            {
                partyBo.UsunKontakt(contact);
                if (!partyBo.Zapisz())
                {
                    throw new InvalidOperationException(NexoValidationMessageBuilder.Build(
                        "Party contact deletion failed:",
                        partyBo.PobierzKomunikatyBledow()));
                }
                return DeletePartyResourceResult.Deleted;
            }
            finally
            {
                partyBo.Odblokuj();
            }
        }
        #endregion
    }
}
