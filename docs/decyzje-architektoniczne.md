# Decyzje architektoniczne

## Kontekst problemu

InsERT nexo dobrze wspiera integracje budowane bezpośrednio w .NET przez SDK i Sferę. Problem pojawia się wtedy, gdy z tych samych danych chcą korzystać prostsze narzędzia, skrypty lub aplikacje, które nie powinny znać SDK nexo ani być z nim bezpośrednio sprzężone.

SubiektNexoConnector wyznacza granicę między światem nexo a zewnętrznymi konsumentami. Udostępnia lokalny kontrakt HTTP dla wybranych scenariuszy odczytu i zapisu, tłumacząc publiczne żądania na operacje SDK oraz mapując model dostawcy na własne DTO.

Dokument rozróżnia decyzje już obowiązujące od planowanych kierunków. Opis planu nie oznacza, że dana funkcja jest już dostępna w API.

## Decyzje obowiązujące

### 1. Osobna granica między nexo a konsumentami

Connector nie udostępnia modelu nexo bezpośrednio. Publiczne komendy, zapytania i DTO należą do adaptera, natomiast typy SDK pozostają w warstwie infrastruktury. Dzięki temu konsument nie musi znać sesji Sfery, obiektów biznesowych, accessorów pól własnych ani sposobu zapisu danych w nexo.

### 2. Oddzielenie logiki aplikacyjnej od integracji z nexo

Podział na `Api`, `Core` i `Infrastructure` zamyka zależność od SDK w jednym miejscu:

- `Api` odpowiada za HTTP, uwierzytelnianie, serializację i mapowanie statusów,
- `Core` definiuje przypadki użycia, walidację wejścia, komendy, zapytania i porty repozytoriów,
- `Infrastructure` zarządza sesjami Sfery, mapowaniem, blokadami i zapisem obiektów biznesowych.

Pozwala to testować kontrakt HTTP oraz większość reguł aplikacyjnych bez uruchamiania nexo i bez połączenia z jego bazą.

### 3. REST jako pierwszy kontrakt integracyjny

Lokalne REST API jest prostsze do uruchomienia i wykorzystania niż broker wiadomości. Dobrze odpowiada bieżącym scenariuszom zapytań oraz krótkich operacji zapisu. Model asynchroniczny pozostaje możliwym rozszerzeniem dla operacji długotrwałych, ale nie jest wymagany dla każdego przypadku użycia.

### 4. Identyfikatory publiczne zgodne z językiem integracji

Produkty są identyfikowane publicznie przez SKU, a kontrahenci przez sygnaturę. Są to identyfikatory rozpoznawalne przez użytkowników i systemy zewnętrzne, dlatego nie wymagają dodatkowej translacji na wewnętrzne ID nexo.

Wewnętrzne ID są używane tylko tam, gdzie element nie posiada stabilnego identyfikatora biznesowego, na przykład dla adresów i kontaktów należących do kontrahenta. Ich pełna tożsamość wynika wtedy z zasobu nadrzędnego i lokalnego ID:

```text
/parties/{partySignature}/addresses/{addressId}
/parties/{partySignature}/contacts/{contactId}
```

Zmiana SKU lub sygnatury przez `PATCH` oznacza zmianę publicznego adresu zasobu. Odpowiedź zwraca nowy identyfikator, a klient powinien używać go w kolejnych żądaniach.

### 5. Kontrahent jako zasób nadrzędny dla adresów i kontaktów

Adres i kontakt nie są samodzielnymi agregatami API. Ich cykl życia jest związany z kontrahentem, dlatego są wystawione jako zagnieżdżone zasoby `parties`. Operacje przechodzą przez repozytorium kontrahenta i zapisują obiekt biznesowy kontrahenta w Sferze.

### 6. Jawna semantyka częściowych aktualizacji

Żądania `PATCH` rozróżniają trzy stany właściwości:

- właściwość pominięta — pozostaw obecną wartość,
- właściwość z wartością — ustaw nową wartość,
- właściwość przekazana jako `null` — wyczyść pole, jeśli kontrakt na to pozwala.

Do zachowania tej informacji służy `Optional<T>`. Pusty dokument `PATCH` jest odrzucany, a wartości tekstowe i kolekcje identyfikatorów są normalizowane przed wywołaniem repozytorium. Dzięki temu warstwa infrastruktury otrzymuje jednoznaczną komendę, bez odgadywania intencji klienta.

### 7. Metadane pól dodatkowych jako osobny zasób

Definicje prostych i zaawansowanych pól własnych są konfigurowalne w nexo i mogą różnić się pomiędzy wdrożeniami. Nie powinny więc być na stałe zakodowane w DTO produktu lub kontrahenta.

Osobny zasób definicji pozwala klientowi odkryć:

- dostępne pola i ich typy,
- wymagalność, widoczność i edytowalność,
- grupy pól zaawansowanych,
- precyzję oraz ograniczenia prezentacji,
- słowniki własne, SQL i wybrane słowniki systemowe,
- dostępne flagi i ich domeny.

Wartości pól są zwracane przy konkretnym produkcie lub kontrahencie, natomiast ich definicje są pobierane niezależnie.

### 8. Pola dodatkowe i flagi jako mechanizm uniwersalności adaptera

Różne instalacje nexo rozszerzają podstawowe kartoteki w inny sposób. Obsługa pól prostych, pól zaawansowanych, słowników i flag pozwala adapterowi przenosić dane specyficzne dla wdrożenia bez rozbudowywania publicznego kontraktu przy każdej zmianie konfiguracji.

Jest to świadomy kompromis: część kontraktu pozostaje dynamiczna i wymaga od klienta odczytania metadanych. W zamian adapter nie ogranicza integracji wyłącznie do zestawu pól przewidzianego podczas jego kompilacji.

### 9. Oficjalne SDK zamiast bezpośrednich zapytań do bazy

Dostęp przez SDK jest wolniejszy i mniej bezpośredni niż własne zapytania SQL, ale respektuje granice platformy nexo, walidację oraz cykl życia obiektów biznesowych. Adapter pozostaje dzięki temu mniej zależny od wewnętrznej struktury bazy danych.

Modyfikacja istniejącego obiektu używa blokady biznesowej `Zablokuj()` i zwalnia ją w `finally`. Ta blokada chroni zapis konkretnego obiektu w nexo i nie zastępuje ograniczania współbieżności całego SDK.

### 10. Błędy API niezależne od wyjątków dostawcy

Błędy walidacji Sfery są tłumaczone na czytelne komunikaty i odpowiedzi `ProblemDetails`. Kontrakt HTTP nie ujawnia typów wyjątków SDK. Brak zasobu, niepoprawne żądanie i konflikt biznesowy powinny mieć odrębne statusy HTTP.

## Dostęp do Sfery i kontrola współbieżności

### Stan obecny

Operacje repozytoriów wykonują synchroniczne API dostawcy przez współdzielony `ISferaExecutor`. Executor jest singletonem procesu i dopuszcza tylko jedną aktywną operację Sfery naraz. Jego zakres obejmuje utworzenie uchwytu, logowanie, pracę z SDK oraz `Dispose()` uchwytu.

Decyzję potwierdził test równoległych odczytów: równoległe wywołania `MenedzerPolaczen.Polacz()` kończyły się niedeterministycznymi błędami wewnątrz Sfery. Sam fakt, że część żądań czasem kończyła się powodzeniem, nie jest gwarancją bezpiecznej współbieżności SDK.

### Pierwszy etap: konfigurowalna bramka dostępu

Współdzielona bramka jest oparta na `SemaphoreSlim` z limitem `1`. Obejmuje wyłącznie pracę z SDK, a nie uwierzytelnianie, walidację żądania ani serializację odpowiedzi. Następne kroki obserwowalności to pomiary:

- czasu oczekiwania na wejście,
- czasu wykonywania operacji Sfery,
- liczby oczekujących operacji,
- timeoutów i błędów blokad,
- percentyli czasu odpowiedzi, szczególnie p95 i p99.

Oczekiwanie na bramkę jest ograniczone konfiguracją `Nexo:SferaExecution:QueueTimeoutSeconds` (domyślnie 30 sekund). Po jej przekroczeniu API odpowiada `503 Service Unavailable` oraz nagłówkiem `Retry-After`, którego wartość kontroluje `Nexo:SferaExecution:RetryAfterSeconds` (domyślnie 5 sekund). Każda zakończona operacja zapisuje w logu ustrukturyzowane pola czasu oczekiwania, czasu wykonania, głębokości kolejki i wyniku.

`SemaphoreSlim` ogranicza równoległość w pojedynczym procesie, ale nie koordynuje wielu instancji API i nie gwarantuje wykonania kolejnych operacji na tym samym wątku. Jeżeli przyszłe testy wykażą zależność Sfery od stałego wątku, następnym etapem będzie pojedynczy worker.

### Drugi etap: kolejka i dedykowany worker

Jeżeli SDK okaże się zależne od konkretnego wątku, wymaga STA albo semafor nie zapewni wystarczającej kontroli, operacje mogą zostać skierowane do kolejki obsługiwanej przez pojedynczego workera. Naturalnym pierwszym krokiem jest ograniczony `Channel<T>` z jednym konsumentem wewnątrz procesu API.

Worker może posiadać własny wątek i kontrolować pełny cykl życia sesji Sfery. Ograniczona pojemność kanału zapewnia backpressure i zapobiega niekontrolowanemu wzrostowi użycia pamięci.

Osobny proces workera ma sens dopiero wtedy, gdy potrzebne są:

- trwała kolejka i odporność na restart API,
- ponawianie operacji,
- harmonogramy lub długotrwałe zadania,
- niezależne skalowanie części HTTP i części SDK,
- odpowiedzi `202 Accepted` oraz osobny zasób statusu zadania.

Worker nie zwiększy automatycznie przepustowości, jeżeli Sfera i tak musi wykonywać operacje sekwencyjnie. Jego główną wartością jest kontrola kolejki, izolacja, przewidywalność i odporność.

### Kryteria przejścia do kolejnego etapu

Zmiana modelu wykonania powinna wynikać z danych. Sygnałami do wprowadzenia bramki albo workera są:

- potwierdzone wymaganie jednego wątku lub STA,
- błędy pojawiające się tylko przy równoległych sesjach,
- przekroczenie ustalonego budżetu p95 czasu odpowiedzi,
- rosnący czas oczekiwania na dostęp do SDK,
- operacje utrzymujące połączenie HTTP przez zbyt długi czas,
- potrzeba trwałych retry lub wznowienia pracy po restarcie.

## Planowany cache i odświeżanie danych

### Jawne rozróżnienie danych `live` i `cached`

Kosztowne odczyty, takie jak duże listy, stany lub rozbudowane szczegóły, mogą docelowo korzystać z cache. Klient musi jednak wiedzieć, czy otrzymał dane bezpośrednio ze Sfery, czy kopię o kontrolowanej świeżości.

Odpowiedź albo jej metadane powinny przekazywać co najmniej:

- źródło `live` lub `cached`,
- czas pobrania danych ze Sfery,
- opcjonalnie maksymalny akceptowany wiek danych.

Operacja zapisu powinna aktualizować albo unieważniać powiązane wpisy cache. Brak tej reguły prowadziłby do sytuacji, w której API po poprawnym zapisie zwraca nieaktualny odczyt.

### Worker interwałowy dla systemów o małym ruchu

Dla systemów o małym ruchu prostszy od infrastruktury eventowej może być worker odświeżający wybrane dane w określonych interwałach. Pozwala on przenieść koszt odczytu poza ścieżkę żądania i utrzymywać przewidywalny maksymalny wiek danych.

Interwały powinny być konfigurowalne osobno dla różnych kategorii danych. Stany magazynowe mogą wymagać częstszego odświeżania niż definicje pól dodatkowych. Mechanizm powinien także umożliwiać ręczne wymuszenie odświeżenia oraz pomijać uruchomienie kolejnej instancji zadania, jeżeli poprzednia nadal trwa.

Wdrożenie brokera wiadomości nie jest konieczne, dopóki wystarcza pojedynczy worker, odtwarzalny cache i okresowe odświeżanie.

## Limity i backpressure

Adapter powinien jawnie ograniczać koszt pojedynczego klienta oraz maksymalną ilość pracy oczekującej na SDK. Limity są częścią kontraktu operacyjnego, a nie tylko optymalizacją implementacji.

Zakładane mechanizmy obejmują:

- maksymalny `pageSize` dla endpointów listujących,
- limit czasu oczekiwania na dostęp do Sfery,
- limit czasu wykonania operacji,
- ograniczoną pojemność przyszłej kolejki,
- limit wielkości kolekcji i operacji zbiorczych,
- opcjonalny rate limiting dla klientów API.

Przekroczenie limitu wejściowego powinno zwracać `400 Bad Request`. Przepełnienie kolejki lub chwilowy brak przepustowości powinny prowadzić do `429 Too Many Requests` albo `503 Service Unavailable`, wraz z informacją umożliwiającą ponowienie żądania.

Wartości limitów powinny być konfigurowalne i widoczne w metrykach. Zbyt duże, ukryte kolejki pogarszają czas odpowiedzi i maskują przeciążenie zamiast je rozwiązywać.

## Identyfikacja żądań i ochrona przed duplikatami

Identyfikator korelacyjny oraz klucz idempotencji rozwiązują dwa różne problemy:

- request/trace ID służy do śledzenia żądania w logach,
- `Idempotency-Key` zapobiega wielokrotnemu wykonaniu tej samej operacji zapisu.

Samo `X-Request-Id` nie chroni przed duplikatami. Dla wszystkich biznesowych operacji `POST`, `PUT`, `PATCH` i `DELETE` klient integracyjny musi przesłać `Idempotency-Key`. Adapter zapisuje wtedy:

- klucz i tożsamość klienta,
- metodę i ścieżkę,
- skrót istotnej części żądania,
- status oraz treść odpowiedzi,
- czas wygaśnięcia wpisu.

Ponowienie identycznego żądania z tym samym kluczem zwraca wcześniej zapisany wynik bez ponownego wywołania Sfery. Użycie tego samego klucza dla innej treści powinno zakończyć się `409 Conflict`.

Magazyn idempotencji musi przeżyć co najmniej typowe ponowienia klienta. Dla pilota wybrano trwały, lokalny magazyn SQLite per instancja adaptera, z unikalnym indeksem `(ClientId, Key)`. Pamięć procesu nie zastępuje tego magazynu, ponieważ nie chroni po restarcie. Lokalny plik SQLite nie stanowi też współdzielonej gwarancji dla niezależnych procesów korzystających z różnych baz.

### Automatyczna polityka dla zapisów HTTP

Decyzja z 2026-09-18: wszystkie endpointy kontrolerów korzystające z `POST`, `PUT`, `PATCH` lub `DELETE` automatycznie wymagają uwierzytelnionej tożsamości oraz `Idempotency-Key`. Dotyczy to również adresów i kontaktów. Wspólna `IdempotencyPolicy` jest używana przez middleware i Swagger; nie trzeba pamiętać o atrybucie na każdej metodzie. Odczyty oraz techniczne minimal endpoints, takie jak `/maintenance/stop`, są poza tą polityką. Kontrolery stanowią obecnie granicę biznesowego API; nowy kontroler techniczny wymaga świadomego dostosowania polityki.

Chroniony zapis musi przechodzić przez `SferaExecutor` i kończyć się w ramach żądania HTTP. `Executing` jest utrwalany przed callbackiem biznesowym, a `Completed` wraz z odpowiedzią przed wysłaniem jej klientowi. Powiązanie ze Sferą, odtwarzanie odpowiedzi i odzyskiwanie po restarcie są zaimplementowane; testy akceptacyjne na żywej Sferze pozostają wymagane.

Niepewny wynik zwraca `409` z kodem `idempotency_result_indeterminate`. Dla POST klient sprawdza listę zasobów, dla PATCH/PUT aktualne dane, a dla DELETE to, czy zasób nadal istnieje. Nie generuje automatycznie nowego klucza. Idempotencja nie zapewnia kolejności różnych żądań ani kontroli współbieżnych edycji. Koszt lokalnego magazynu jest akceptowany w zamian za brak ponownego wejścia do Sfery i odtworzenie pierwotnej odpowiedzi, również dla operacji naturalnie idempotentnych.

### Rozłączenie klienta po przyjęciu zapisu

Decyzja z 2026-09-19: po utworzeniu trwałego wpisu `Pending` i ostatnim sprawdzeniu anulowania dalsze wykonanie chronionego zapisu nie zależy od połączenia klienta. Middleware zachowuje oryginalny `HttpContext.RequestAborted`, a na czas kontrolera, wykonania w Sferze i serializacji odpowiedzi zastępuje go tokenem, który nie jest anulowany przez rozłączenie. Zapobiega to utrwaleniu `Completed` ze statusem i nagłówkami, ale z uciętym lub pustym body, gdy timeout klienta nastąpi podczas serializacji.

Odpowiedź jest najpierw w całości budowana w lokalnym buforze i zapisywana w SQLite. Dopiero potem middleware przywraca token klienta i próbuje skopiować gotową odpowiedź do połączenia. Anulowanie tego końcowego kopiowania jest oczekiwanym rozłączeniem, które zostaje odnotowane informacyjnie, ale nie trafia do globalnej obsługi jako błąd serwera. Nieudane wysłanie nie cofa `Completed`; retry z tym samym kluczem odtwarza pełną odpowiedź. Jest to świadoma semantyka: po przyjęciu zapisu zmiana zachowania klienta nie anuluje operacji biznesowej. Limity kolejki i błędy serwera nadal obowiązują niezależnie od tokenu klienta.

Odłączenie dotyczy wyłącznie żywotności połączenia HTTP. Twarde zakończenie procesu nadal przerywa pracę: `Executing` zostanie po restarcie oznaczone jako `Indeterminate`, a przerwane `Pending` usunięte. Oryginalny token jest używany przy końcowym kopiowaniu odpowiedzi, więc serwer nie próbuje podtrzymywać martwego połączenia.

### Pamięć jako pierwsze sito idempotencji — opcja odroczona

Decyzja z 2026-09-18: nie dodajemy obecnie cache idempotencji w pamięci. Przy zakładanym ruchu około 1000 żądań na godzinę nie mamy pomiarów wskazujących, że dostęp do SQLite jest wąskim gardłem. Dodatkowy cache wprowadza koszt synchronizacji stanów, wygaszania i unieważniania wpisów. Trwała bramka HTTP, powiązanie stanów ze Sferą, odtwarzanie odpowiedzi i odzyskiwanie po restarcie są zaimplementowane bez dodatkowej warstwy pamięci.

Warstwę pamięci można rozważyć jako pierwsze sito duplikatów w przypadku wyjątkowo niestabilnych kontraktów lub integracji: częstych timeoutów, zerwanych połączeń i ponowień identycznych żądań z tym samym kluczem w krótkich seriach. Nie chodzi o zmienny payload pod tym samym kluczem — takie żądanie nadal musi zostać odrzucone jako konflikt. Podstawą decyzji powinny być pomiary udziału duplikatów, chwilowych skoków ruchu, czasu dostępu do SQLite i liczby błędów blokady, a nie sama średnia liczba żądań na godzinę.

Jeśli cache zostanie wprowadzony, obowiązują następujące zasady:

- Kluczem cache jest `(ClientId, Key)`, a wpis zawiera również `RequestHash`. Trafienie z innym hashem nie może ominąć kontroli konfliktu.
- Brak wpisu w pamięci prowadzi do próby `INSERT` w SQLite. Dopiero potwierdzony zapis nowej operacji stanowi bilet wstępu do wykonania operacji biznesowej w Sferze.
- Wpis nowej operacji trafia do pamięci dopiero po potwierdzonym zapisie w SQLite. Konflikt unikalności wymaga odczytu istniejącej operacji i porównania żądania; nie daje zgody na ponowne wykonanie.
- Dwa równoległe żądania mogą oba ominąć pusty cache. Ostateczną ochroną pozostaje unikalny indeks SQLite, nie obecność wpisu w pamięci.
- Zmiana stanu w pamięci następuje po potwierdzonym zapisie stanu w SQLite. Przy awarii zapisu odpowiedni wpis cache należy unieważnić.
- Cache przechowuje niemutowalne snapshoty, nie `DbContext` ani śledzone encje EF Core. Ma ograniczony rozmiar i czas życia; jego utrata lub wygaszenie powoduje powrót do SQLite, nie utratę ochrony.
- Najprostszy wariant przechowuje tylko aktywne operacje, aby szybko odrzucać ich duplikaty. Odtwarzanie ukończonych odpowiedzi pozostaje w SQLite; cache odpowiedzi jest osobną, późniejszą optymalizacją.

SQLite pozostaje źródłem prawdy. Nie zastępujemy trwałych zapisów asynchronicznym flushowaniem stanu z pamięci: awaria po zapisie w Nexo, ale przed utrwaleniem idempotencji mogłaby dopuścić duplikat po restarcie. Docelowy mechanizm potwierdza `Executing` przed operacją biznesową i utrwala `Completed` wraz z odpowiedzią przed wysłaniem jej klientowi. Jest to optymalizacja liczby odczytów, a nie druga niezależna gwarancja idempotencji ani zamiennik transakcji po stronie Nexo.

Idempotencja nie zastępuje transakcyjności operacji po stronie nexo. Chroni przede wszystkim przed ponownym wykonaniem żądania, gdy klient nie otrzymał odpowiedzi i nie wie, czy pierwsza próba się powiodła.

## Trade-offy

### Prostota synchronicznego REST kontra kolejka

Synchroniczne API jest łatwe dla klienta i dobrze pasuje do krótkich operacji. Kolejka daje lepszy backpressure i odporność, ale wprowadza statusy zadań, trwałość komunikatów i bardziej złożoną diagnostykę. Dlatego przejście do workera powinno wynikać z ograniczeń SDK lub pomiarów, a nie z założenia na starcie.

### Uniwersalne pola kontra silnie typowany kontrakt

Dynamiczne pola i flagi pozwalają obsługiwać różne konfiguracje nexo bez wdrażania nowej wersji adaptera. Klient traci jednak część wygody silnego typowania i musi korzystać z zasobu metadanych.

### Cache kontra świeżość

Cache skraca czas odpowiedzi i ogranicza liczbę sesji SDK kosztem kontrolowanego opóźnienia danych. Jawne oznaczenie źródła i czasu pobrania ma zapobiegać ukrywaniu tego kompromisu przed konsumentem.

### SDK kontra bezpośredni SQL

SDK nakłada koszt wydajnościowy i ograniczenia wykonania, ale zachowuje reguły platformy oraz zmniejsza zależność od schematu bazy. Bezpośrednie zapytania SQL nie są obecnie planowaną ścieżką optymalizacji.

## Kolejność dalszego rozwoju

1. Dodać pomiary czasu operacji Sfery, oczekiwania oraz błędów blokad.
2. Wprowadzić jawne limity wejściowe, timeouty i identyfikatory korelacyjne.
4. Dodać idempotencję dla operacji zapisu.
5. Wprowadzić cache z metadanymi `live`/`cached` oraz poprawną invalidacją po zapisie.
6. Dodać prosty worker interwałowy dla kosztownych odczytów w systemach o małym ruchu.
7. Przejść do kolejki z dedykowanym wątkiem lub osobnego procesu workera tylko po potwierdzeniu wymagań technicznych.

## English Summary

SubiektNexoConnector is a local HTTP boundary between InsERT nexo and consumers that should not depend on the vendor SDK directly.

Key decisions:

- keep commands, queries and DTOs independent of the nexo model,
- use SKU and party signature as public business identifiers,
- model addresses and contacts as nested party resources,
- preserve omitted/value/null semantics for PATCH requests,
- expose configurable custom-field and flag metadata separately from entity values,
- use the official SDK instead of direct SQL access.

The current execution model uses synchronous SDK calls. Concurrency limits, an SDK execution gate and a dedicated worker are evolutionary steps that should be introduced only after measuring real behavior. `SemaphoreSlim` can limit parallel calls but does not guarantee thread affinity; a single-reader queue and dedicated thread remain options if required by Sfera.

The planned cache will explicitly identify `live` and `cached` data. Low-traffic installations may use an interval-based refresh worker instead of event infrastructure. Request correlation IDs are intended for diagnostics, while a separate durable `Idempotency-Key` mechanism protects controller POST, PUT, PATCH and DELETE operations against repeated execution.
