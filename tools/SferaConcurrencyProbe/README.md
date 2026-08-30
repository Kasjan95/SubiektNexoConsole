# Scenariusz obciążeniowy Sfery

Narzędzie wykonuje wyłącznie odczyty. Każdy wirtualny klient pobiera najpierw listę produktów, a następnie równolegle pobiera detale kilku wybranych SKU. Dzięki temu test obejmuje zarówno listowanie, jak i cięższe detale produktów.

Każdy poziom równoległości jest uruchamiany dwukrotnie: bez `X-Correlation-Id` i z nim. W drugim wariancie wszystkie wywołania jednego wirtualnego klienta mają ten sam UUID. W pierwszym wariancie probe weryfikuje, że adapter sam zwraca poprawny UUID w każdej odpowiedzi.

Domyślne poziomy równoległości to `4`, `8`, `16`; każdy klient wykonuje listę oraz `5` pobrań detali. Przy wyższych poziomach celowo powinny pojawić się odpowiedzi `503`, jeżeli czas oczekiwania w kolejce przekroczy `Nexo:SferaExecution:QueueTimeoutSeconds`.

Najpierw uruchom API ze wskazaniem konfiguracji połączenia:

```powershell
dotnet run --project src\SubiektNexoConnector.Api\SubiektNexoConnector.Api.csproj -- --config
```

W drugim oknie PowerShell uruchom próbę:

```powershell
dotnet run --project tools\SferaConcurrencyProbe
```

Domyślny adres API to `https://localhost:7214/`. Jeżeli API wymaga klucza, ustaw go tylko w bieżącej sesji:

```powershell
$env:SUBIEKT_NEXO_CONNECTOR_API_KEY = "..."
dotnet run --project tools\SferaConcurrencyProbe
```

Przykład z trzema powtórzeniami i trzema detalami na klienta:

```powershell
dotnet run --project tools\SferaConcurrencyProbe -- --repetitions 3 --details-per-run 3
```

Parametry:

- `--base-uri URL` — adres bazowy API; `--uri` pozostaje aliasem.
- `--concurrency 4,8,16` — poziomy równoległości.
- `--details-per-run 5` — liczba równoległych detali po liście produktów.
- `--products-page-size 50` — liczba produktów pobieranych do wyboru detali.
- `--timeout-seconds 60` — timeout jednego wywołania HTTP probe.

Wynik rozróżnia powodzenie całych workflow oraz pojedynczych wywołań HTTP. Kod zakończenia `1` oznacza, że co najmniej jeden workflow nie zakończył się powodzeniem.
