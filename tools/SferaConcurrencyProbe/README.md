# Test równoległych odczytów Sfery

Narzędzie wysyła w tej samej chwili wyłącznie żądania HTTP `GET`, aby sprawdzić zachowanie API i Sfery przy równoległości `2`, `4` i `8`. Nie wykonuje zapisów w nexo.

Najpierw uruchom API ze wskazaniem konfiguracji połączenia:

```powershell
dotnet run --project src\SubiektNexoConnector.Api\SubiektNexoConnector.Api.csproj -- --config
```

W drugim oknie PowerShell uruchom próbę na lekkim endpointzie odczytowym:

```powershell
dotnet run --project tools\SferaConcurrencyProbe
```

Domyślny adres to `https://localhost:7214/warehouses`, a poziomy równoległości to `2,4,8`. Jeżeli API wymaga klucza, ustaw go tylko w bieżącej sesji:

```powershell
$env:SUBIEKT_NEXO_CONNECTOR_API_KEY = "..."
dotnet run --project tools\SferaConcurrencyProbe
```

Przykład z dziesięcioma powtórzeniami każdego poziomu:

```powershell
dotnet run --project tools\SferaConcurrencyProbe -- --repetitions 10
```

Endpoint można zmienić wyłącznie na inny endpoint `GET`:

```powershell
dotnet run --project tools\SferaConcurrencyProbe -- --uri "https://localhost:7214/products?page=1&pageSize=10" --repetitions 10
```

Wynik podaje liczbę odpowiedzi `2xx`, czasy pojedynczych żądań oraz czas całej serii. Kod zakończenia `1` oznacza, że co najmniej jedno żądanie nie zakończyło się odpowiedzią `2xx`.
