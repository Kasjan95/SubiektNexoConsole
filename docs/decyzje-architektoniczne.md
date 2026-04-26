# Decyzje architektoniczne

## Kontekst problemu

InsERT nexo dobrze wspiera integracje budowane bezpośrednio w .NET przez SDK i Sferę. Problem pojawia się wtedy, gdy z tych samych danych chcą korzystać prostsze narzędzia, skrypty albo aplikacje, które nie powinny znać SDK nexo ani być z nim bezpośrednio sprzężone.

W tym projekcie nie chodzi więc tylko o pobranie danych z systemu ERP. Chodzi o wyznaczenie sensownej granicy między światem nexo a zewnętrznymi konsumentami oraz udostępnienie im prostszego, lokalnego kontraktu HTTP dla wybranych scenariuszy odczytu.

## Kluczowe decyzje

### 1. Osobna granica między nexo a konsumentami

Connector nie udostępnia modelu nexo bezpośrednio. Jego rolą jest tłumaczenie danych i wystawienie prostszego kontraktu dla zewnętrznych narzędzi.

### 2. Oddzielenie logiki aplikacyjnej od integracji z nexo

Podział na `Api`, `Core` i `Infrastructure` pozwala zamknąć zależność od SDK w jednym miejscu. Dzięki temu logika przypadków użycia nie jest bezpośrednio sprzężona z technicznymi szczegółami dostawcy.

### 3. Skupienie pierwszej wersji na odczycie danych

Pierwsza wersja projektu koncentruje się na scenariuszach odczytu, takich jak produkty, magazyny, stany i ceny. To świadome zawężenie zakresu, które pozwala najpierw ustabilizować granicę integracyjną i kontrakt API.

### 4. Identyfikacja produktów po SKU zamiast po wewnętrznym ID

W kontrakcie integracyjnym produkty są identyfikowane po SKU, a nie po wewnętrznym identyfikatorze nexo. To lepiej odpowiada rzeczywistemu sposobowi pracy systemów zewnętrznych, które synchronizują się po SKU lub EAN, więc wprowadzanie translacji na lokalne ID nie wnosi wartości do kontraktu.

## Trade-offy

### REST zamiast modelu eventowego

Na obecnym etapie wybrałem lokalny kontrakt HTTP zamiast integracji opartej o RabbitMQ lub Kafka. To upraszcza uruchomienie i pozwala szybciej zweryfikować granicę integracyjną, ale gorzej wspiera scenariusze asynchroniczne i dalszą propagację zdarzeń.

### SDK zamiast bezpośrednich zapytań do bazy

Dostęp do danych przez SDK jest wolniejszy i mniej bezpośredni niż własne zapytania SQL, ale lepiej respektuje granice platformy nexo. Dzięki temu integracja jest mniej zależna od wewnętrznej struktury bazy i bardziej odporna na zmiany po stronie dostawcy.

### Cache zamiast zawsze żywego odczytu

Bezpośredni odczyt przez SDK daje dane na żywo, ale czas odpowiedzi dla żądań `GET` jest wysoki. Docelowym kierunkiem jest więc warstwa cache, która skraca czas odpowiedzi kosztem zaakceptowania kontrolowanego opóźnienia danych, zamiast za każdym razem pobierać wszystko synchronicznie albo budować osobny kontrakt typu eksport/zlecenie przetwarzania.

## Kierunki rozwoju

- Warstwa cache dla kosztownych odczytów i cięższych endpointów.
- Osobny worker do odświeżania danych, eksportów i przyszłych zadań asynchronicznych.
- Rozszerzenie API o kolejne zasoby oraz scenariusze wykraczające poza same `GET`.
- Rozwój kontraktu API o filtrowanie, paginację i lżejsze modele odpowiedzi.
- Lepsza diagnostyka przepływu integracyjnego, w tym rozróżnienie odpowiedzi `live` i `cache`.

## English Summary

This project acts as a boundary between InsERT nexo and external consumers that should not depend on the nexo SDK directly.

Key decisions:

- expose a simplified local HTTP contract instead of leaking the vendor model,
- keep application logic separated from nexo-specific integration details,
- focus the first version on read scenarios,
- identify products by SKU instead of internal IDs.

Main trade-offs:

- REST was chosen over an event-driven model for the first stage,
- the integration uses the official SDK instead of direct SQL queries,
- cache is the preferred direction for expensive reads, even at the cost of delayed freshness.
