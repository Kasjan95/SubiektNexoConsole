# Ręczna aktualizacja adaptera i wersjonowanie

## Jedno źródło wersji

`Release.props` w katalogu repozytorium zawiera czteroczłonowy `ReleaseVersion`, obecnie `1.2.0.0`. Wspólny import w `Directory.Build.props` ustawia tę samą wersję projektów, AssemblyVersion i FileVersion. MPKG otrzymuje wersję z faktycznej DLL adaptera.

Zwykłe uruchomienie `scripts/Publish-Deployment.ps1` pyta o poziom zmiany i pokazuje proponowany wynik. Dla `1.2.3.4`:

| Wybór | Wynik |
| --- | --- |
| 1. Duża wersja | 2.0.0.0 |
| 2. Nowe funkcje | 1.3.0.0 |
| 3. Poprawki | 1.2.4.0 |
| 4. Drobna rewizja | 1.2.3.5 |
| 5. Użyj obecnej | 1.2.3.4 |

Przed zmianą skrypt prosi o potwierdzenie. Q anuluje wybór. Potwierdzony numer zapisuje atomowo do Release.props **przed budowaniem**; pozostaje on zapisany także po nieudanym publishu. Aby ponowić próbę bez kolejnego podbicia, wybierz 5. Każdy człon jest ograniczony do 65534 ze względu na wersję assembly .NET.

Bez pytań, np. w automatyzacji: `scripts/Publish-Deployment.ps1 -VersionLevel Patch -Yes`. Dostępne wartości: Major, Minor, Patch, Revision, Current. `-Yes` pomija potwierdzenie, ale bez VersionLevel nadal pojawi się menu. Tryb Production po zmianie wersji wymaga jej commita: zapisz/commituj numer, następnie uruchom z `-VersionLevel Current` i parametrami podpisu. Skrypt nigdy sam nie commituje.

`scripts/Publish-Deployment.ps1` buduje adapter i Setup ze wspólnym SourceRevisionId. Publikacja z niezacommitowanymi zmianami jest oznaczana `.dirty`; tryb Production ją odrzuca i nadal wymaga podpisu. Endpoint `/maintenance/info` (z API key) zwraca `version` oraz `informationalVersion`, która zawiera identyfikator commita. Ten identyfikator nie jest kryptograficznym dowodem integralności procesu; pakiet w lokalnym cache jest dodatkowo sprawdzany SHA-256.

Numer Nexo jest osobnym wymaganiem pakietu i przedmiotem testów zgodności, nie numerem wydania adaptera. Nie podnosimy deklaracji zgodności tylko dlatego, że zbudowano nowszy Setup.

Publikacja do katalogu zawierającego tę samą lub nowszą wersję release.json jest odrzucana. To lokalna ochrona przed nadpisaniem wydania, nie globalny rejestr wersji: przy publikowaniu do innych katalogów nadal trzeba pilnować zasady „inne binaria = nowy numer”. Raz opublikowanego wydania nie przebudowujemy do dystrybucji pod tym samym numerem.

## Użytkownik

1. Uruchom nowszy Setup.exe i wybierz zarządzaną instancję → **Aktualizuj / wznów aktualizację**.
2. Potwierdź połączenie z dotychczasowym serwerem. Podmiot nie może zostać przełączony.
3. Sprawdź numer obecnej/docelowej wersji i zatwierdź przerwę w pracy.
4. Instalator zachowa settings.json (bez ponownej serializacji), API key, konto, zadanie, regułę zapory i scenariusz. Nie zmienia opcji Seq ani operatora podczas aktualizacji.
5. Po wdrożeniu uruchamia istniejące pełne zadanie Windows i sprawdza health, zasób Sfery oraz rzeczywistą wersję i identyfikator wydania. Dopiero wtedy zatwierdza nowe metadane.

Nie wykonujemy ręcznego ani automatycznego uninstalla. Używamy `PackageDeployer.Deploy` dla wskazanego podmiotu, zachowując dotychczasową rodzinę pakietu: np. `Pilot-1.0.0.2` → `Pilot-1.2.0.0`, a nie przenosząc stary pakiet do nowej przestrzeni nazw v2. Scenariusz i argument `--instance` pozostają stałe.

Podstawa kontraktu: lokalny przykład InsERT `D:\NexoSDK\nexoSDK_60.0.1.9220\Przyklady\OperacjeSferyczne\Konfiguracja.xml` opisuje wspólną instalację/aktualizację, a oficjalny szablon QuickInstaller (`artifacts/quickinstaller-template/src/PackageDeployerAdapter.cs`) udostępnia Deploy dla wskazanych baz. Nie napisano własnego protokołu SQL ani mechanizmu rozpakowywania na serwer.

## Przerwanie i stare instalacje

- Zapis `PendingUpdate` przechowuje dokładny pakiet, SHA-256, docelową wersję i wynik Deploy. Po przerwaniu wznawiamy ten sam pakiet z cache, nie podmieniamy go payloadem kolejnego instalatora.
- Gdy Deploy zakończył się niepewnie, ponowne wysłanie wymaga jawnego zaznaczenia potwierdzenia w podsumowaniu. Nie oznaczamy takiej operacji sukcesem na podstawie samego health.
- Brak starego settings.json blokuje aktualizację: należy odzyskać konfigurację. Aktualizacja nie tworzy nowego klucza w zastępstwie utraconego.
- Naprawa i odinstalowanie są blokowane przy oczekującej aktualizacji, aby nie działały na nieaktualnym zestawie metadanych. „Napraw / wznów” przekierowuje taką instancję do wznowienia aktualizacji.
- Stary adapter bez `/maintenance/info` trzeba ręcznie zamknąć, a następnie wznowić. Nie zabijamy dowolnego procesu zajmującego port. To zamknięcie procesu, nie uninstall.
- Aktualizacja wymaga prawidłowego istniejącego zadania Password dla tego samego konta. Nie konwertuje sama S4U. Niezarządzany pilot musi zostać wcześniej przejęty; pozostałe ograniczenia importera opisuje `pilot-migration-review.md`.
- Nie ma automatycznego rollbacku. Stary MPKG i chroniona kopia konfiguracji pozostają do diagnostyki/odzyskania, ale ich obecność nie oznacza automatycznej obsługi downgrade'u.

## Weryfikacja

Testy obejmują politykę wersji, niezmienną rodzinę/scenariusz MPKG, zapis oczekującej aktualizacji po błędzie, ochronę metadanych do momentu zaliczenia testu oraz wykrywanie niezgodnej wersji/revizji procesu przez symulowane HTTP. Test API sprawdza metadane faktycznie zbudowanej biblioteki.

Weryfikacja lokalna 2026-09-09: 88 testów Deployment, 119 API i 52 Core zaliczone (259 łącznie); 4 testy systemowe Windows pominięte. Dodano 16 przypadków testowych aktualizacji i rozszerzono test odpowiedzi maintenance/info. Zbudowano `artifacts/deployment-1.2.0-candidate/SubiektNexoConnector.Setup.exe`: jedna aplikacja 31,2 MiB, niepodpisana, źródło oznaczone `.dirty`. FileVersion adaptera i Setup: 1.2.0.0; zgodne ProductVersion z commitem. Oficjalny komponent InsERT poprawnie odczytał wygenerowany z publishu pakiet `Pilot-1.2.0.0` i jeden scenariusz `SubiektNexoConnector.Pilot`. Nie wykonano Deploy. Widok strony instancji zweryfikowano na renderze WPF bez uruchamiania instalacji.

Przed produkcją pozostaje test na licencjonowanym podmiocie: aktualizacja rzeczywistym Deploy, ponowne uruchomienie przez InsLauncher/zadanie Password, dwie instancje na jednym serwerze, restart i przerwanie po zdalnym sukcesie. Sam odczyt manifestu przez InsERT i zielone testy jednostkowe nie kwalifikują tej ścieżki produkcyjnie.
