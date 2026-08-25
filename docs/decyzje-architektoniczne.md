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

Operacje repozytoriów tworzą uchwyt Sfery na czas pojedynczego przypadku użycia i wykonują synchroniczne API dostawcy. ASP.NET Core może obsługiwać wiele żądań równolegle, ale bezpieczny poziom współbieżności SDK nie został jeszcze potwierdzony pomiarami ani jednoznacznym wymaganiem technicznym.

Nie należy zakładać, że większa liczba równoległych sesji automatycznie zwiększy przepustowość. Może ona zwiększyć obciążenie nexo, powodować konflikty blokad lub ujawnić ograniczenia wątkowe bibliotek dostawcy.

### Pierwszy etap: konfigurowalna bramka dostępu

Pierwszym mechanizmem ochronnym, jeśli potwierdzi się taka potrzeba, będzie współdzielona bramka oparta na `SemaphoreSlim`. Początkowy limit może wynosić `1`, ale powinien być konfigurowalny.

Brama powinna obejmować wyłącznie pracę z SDK, a nie uwierzytelnianie, walidację żądania ani serializację odpowiedzi. Jej wprowadzeniu muszą towarzyszyć pomiary:

- czasu oczekiwania na wejście,
- czasu wykonywania operacji Sfery,
- liczby oczekujących operacji,
- timeoutów i błędów blokad,
- percentyli czasu odpowiedzi, szczególnie p95 i p99.

`SemaphoreSlim` ogranicza równoległość w pojedynczym procesie, ale nie koordynuje wielu instancji API i nie gwarantuje wykonania kolejnych operacji na tym samym wątku.

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

Samo `X-Request-Id` nie chroni przed duplikatami. Dla operacji `POST`, `PATCH` i wybranych `DELETE` klient integracyjny powinien móc przesłać `Idempotency-Key`. Adapter zapisuje wtedy:

- klucz i tożsamość klienta,
- metodę i ścieżkę,
- skrót istotnej części żądania,
- status oraz treść odpowiedzi,
- czas wygaśnięcia wpisu.

Ponowienie identycznego żądania z tym samym kluczem zwraca wcześniej zapisany wynik bez ponownego wywołania Sfery. Użycie tego samego klucza dla innej treści powinno zakończyć się `409 Conflict`.

Magazyn idempotencji musi przeżyć co najmniej typowe ponowienia klienta. Pamięć procesu może wystarczyć jako etap lokalny, ale nie chroni po restarcie ani przy wielu instancjach API. Docelowo potrzebny jest współdzielony lub trwały magazyn z polityką TTL.

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
2. Na podstawie pomiarów ustalić limit współbieżności i ewentualnie wprowadzić `SemaphoreSlim`.
3. Wprowadzić jawne limity wejściowe, timeouty i identyfikatory korelacyjne.
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

The planned cache will explicitly identify `live` and `cached` data. Low-traffic installations may use an interval-based refresh worker instead of event infrastructure. Request correlation IDs are intended for diagnostics, while a separate durable `Idempotency-Key` mechanism is planned to prevent duplicate writes.
