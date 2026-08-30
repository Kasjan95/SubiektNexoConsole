using System.Diagnostics;
using System.Net;
using System.Text.Json;

const string CorrelationIdHeaderName = "X-Correlation-Id";

var options = ProbeOptions.Parse(args);
using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds) };

if (!string.IsNullOrWhiteSpace(options.ApiKey))
    client.DefaultRequestHeaders.Add(options.ApiKeyHeader, options.ApiKey);

Console.WriteLine($"API: {options.BaseUri}");
Console.WriteLine($"Scenariusz: GET /products (pageSize={options.ProductsPageSize}), potem równoległe GET detali {options.DetailsPerRun} produktów.");
Console.WriteLine($"Poziomy równoległości: {string.Join(", ", options.Concurrency)}; powtórzenia: {options.Repetitions}; Correlation ID: bez i z.");

var allBatches = new List<BatchResult>();
foreach (var correlationMode in CorrelationMode.All)
{
    Console.WriteLine();
    Console.WriteLine($"Tryb: {correlationMode.DisplayName}");

    foreach (var concurrency in options.Concurrency)
    for (var iteration = 1; iteration <= options.Repetitions; iteration++)
    {
        var batch = await RunBatchAsync(client, options, correlationMode, concurrency, iteration);
        allBatches.Add(batch);

        Console.WriteLine(
            $"n={concurrency,2}, próba={iteration,2}: workflow {batch.SuccessfulWorkflowCount}/{concurrency} OK, " +
            $"HTTP {batch.SuccessfulCallCount}/{batch.Calls.Count} 2xx, całość={batch.Elapsed.TotalMilliseconds,8:N0} ms, " +
            $"workflow min/p50/max={batch.MinMilliseconds:N0}/{batch.P50Milliseconds:N0}/{batch.MaxMilliseconds:N0} ms, " +
            $"statusy: {batch.StatusSummary}");
    }
}

Console.WriteLine();
Console.WriteLine("Podsumowanie:");
foreach (var correlationMode in CorrelationMode.All)
foreach (var concurrency in options.Concurrency)
{
    var batches = allBatches.Where(batch => batch.CorrelationMode == correlationMode && batch.Concurrency == concurrency).ToList();
    var workflows = batches.SelectMany(batch => batch.Workflows).ToList();
    var calls = workflows.SelectMany(workflow => workflow.Calls).ToList();
    var elapsed = batches.Select(batch => batch.Elapsed.TotalMilliseconds).Order().ToList();

    Console.WriteLine(
        $"{correlationMode.ShortName}, n={concurrency,2}: workflow {workflows.Count(workflow => workflow.IsSuccessful)}/{workflows.Count} OK, " +
        $"HTTP {calls.Count(call => call.IsSuccessStatusCode)}/{calls.Count} 2xx, " +
        $"czas serii min/p50/max={elapsed.First():N0}/{Percentile(elapsed, 0.5):N0}/{elapsed.Last():N0} ms");
}

return allBatches.SelectMany(batch => batch.Workflows).All(workflow => workflow.IsSuccessful) ? 0 : 1;

static async Task<BatchResult> RunBatchAsync(HttpClient client, ProbeOptions options, CorrelationMode correlationMode, int concurrency, int iteration)
{
    var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var workflows = Enumerable.Range(1, concurrency)
        .Select(index => RunWorkflowAsync(client, options, correlationMode, gate, index))
        .ToList();

    var stopwatch = Stopwatch.StartNew();
    gate.SetResult();
    var results = await Task.WhenAll(workflows);
    stopwatch.Stop();

    return new BatchResult(correlationMode, concurrency, iteration, stopwatch.Elapsed, results);
}

static async Task<WorkflowResult> RunWorkflowAsync(HttpClient client, ProbeOptions options, CorrelationMode correlationMode, TaskCompletionSource gate, int index)
{
    await gate.Task.ConfigureAwait(false);
    var stopwatch = Stopwatch.StartNew();
    var correlationId = correlationMode.SendHeader ? Guid.NewGuid().ToString("D") : null;
    var calls = new List<HttpCallResult>();

    var productListCall = await SendGetAsync(
        client,
        new Uri(options.BaseUri, $"products?page=1&pageSize={options.ProductsPageSize}"),
        "lista produktów",
        correlationId);
    calls.Add(productListCall);

    if (!productListCall.IsSuccessStatusCode)
        return new WorkflowResult(index, stopwatch.Elapsed, calls, "Nie udało się pobrać listy produktów.");

    IReadOnlyList<ProductListItem>? products;
    try
    {
        products = JsonSerializer.Deserialize<IReadOnlyList<ProductListItem>>(
            productListCall.Body!, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
    catch (JsonException exception)
    {
        return new WorkflowResult(index, stopwatch.Elapsed, calls, $"Nie udało się odczytać listy produktów: {exception.Message}");
    }

    if (products is null || products.Count == 0)
        return new WorkflowResult(index, stopwatch.Elapsed, calls, "API zwróciło pustą listę produktów.");

    var detailCalls = await Task.WhenAll(SelectProducts(products, options.DetailsPerRun, index).Select(product =>
        SendGetAsync(client, new Uri(options.BaseUri, $"products/{Uri.EscapeDataString(product.SKU)}"), $"detale produktu {product.SKU}", correlationId)));
    calls.AddRange(detailCalls);

    stopwatch.Stop();
    return new WorkflowResult(index, stopwatch.Elapsed, calls, null);
}

static IReadOnlyList<ProductListItem> SelectProducts(IReadOnlyList<ProductListItem> products, int count, int workflowIndex) =>
    Enumerable.Range(0, count).Select(offset => products[(workflowIndex - 1 + offset) % products.Count]).ToList();

static async Task<HttpCallResult> SendGetAsync(HttpClient client, Uri uri, string operation, string? expectedCorrelationId)
{
    var stopwatch = Stopwatch.StartNew();
    try
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (expectedCorrelationId is not null)
            request.Headers.Add(CorrelationIdHeaderName, expectedCorrelationId);

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
        var body = await response.Content.ReadAsStringAsync();
        stopwatch.Stop();

        if (!response.Headers.TryGetValues(CorrelationIdHeaderName, out var values)
            || values.SingleOrDefault() is not { } responseCorrelationId
            || !Guid.TryParse(responseCorrelationId, out _)
            || (expectedCorrelationId is not null && responseCorrelationId != expectedCorrelationId))
        {
            return new HttpCallResult(operation, response.StatusCode, stopwatch.Elapsed, body, "Nieprawidłowy lub brakujący X-Correlation-Id w odpowiedzi.");
        }

        return new HttpCallResult(operation, response.StatusCode, stopwatch.Elapsed, body, null);
    }
    catch (Exception exception)
    {
        stopwatch.Stop();
        return new HttpCallResult(operation, null, stopwatch.Elapsed, null, exception.GetType().Name + ": " + exception.Message);
    }
}

static double Percentile(IReadOnlyList<double> values, double percentile)
{
    var position = (values.Count - 1) * percentile;
    var lower = (int)Math.Floor(position);
    var upper = (int)Math.Ceiling(position);
    return lower == upper ? values[lower] : values[lower] + ((values[upper] - values[lower]) * (position - lower));
}

sealed record ProductListItem(string SKU);

sealed record CorrelationMode(string DisplayName, string ShortName, bool SendHeader)
{
    public static IReadOnlyList<CorrelationMode> All { get; } =
    [
        new("bez X-Correlation-Id", "bez CorId", false),
        new("z X-Correlation-Id", "z CorId", true)
    ];
}

sealed record HttpCallResult(string Operation, HttpStatusCode? StatusCode, TimeSpan Elapsed, string? Body, string? Error)
{
    public bool IsSuccessStatusCode => Error is null && StatusCode is >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices;
}

sealed record WorkflowResult(int Index, TimeSpan Elapsed, IReadOnlyList<HttpCallResult> Calls, string? Error)
{
    public bool IsSuccessful => Error is null && Calls.All(call => call.IsSuccessStatusCode);
}

sealed record BatchResult(CorrelationMode CorrelationMode, int Concurrency, int Iteration, TimeSpan Elapsed, IReadOnlyList<WorkflowResult> Workflows)
{
    public IReadOnlyList<HttpCallResult> Calls => Workflows.SelectMany(workflow => workflow.Calls).ToList();
    public int SuccessfulWorkflowCount => Workflows.Count(workflow => workflow.IsSuccessful);
    public int SuccessfulCallCount => Calls.Count(call => call.IsSuccessStatusCode);
    public double MinMilliseconds => Workflows.Min(workflow => workflow.Elapsed.TotalMilliseconds);
    public double P50Milliseconds => CalculatePercentile(Workflows.Select(workflow => workflow.Elapsed.TotalMilliseconds).Order().ToList(), 0.5);
    public double MaxMilliseconds => Workflows.Max(workflow => workflow.Elapsed.TotalMilliseconds);
    public string StatusSummary => string.Join(", ", Calls.GroupBy(call => call.StatusCode?.ToString() ?? call.Error ?? "unknown").OrderBy(group => group.Key).Select(group => $"{group.Key} ×{group.Count()}"));

    private static double CalculatePercentile(IReadOnlyList<double> values, double percentile)
    {
        var position = (values.Count - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        return lower == upper ? values[lower] : values[lower] + ((values[upper] - values[lower]) * (position - lower));
    }
}

sealed record ProbeOptions(Uri BaseUri, IReadOnlyList<int> Concurrency, int Repetitions, int TimeoutSeconds, int ProductsPageSize, int DetailsPerRun, string ApiKeyHeader, string? ApiKey)
{
    public static ProbeOptions Parse(string[] args)
    {
        var baseUri = new Uri("https://localhost:7214/");
        var concurrency = new List<int> { 4, 8, 16 };
        var repetitions = 1;
        var timeoutSeconds = 60;
        var productsPageSize = 50;
        var detailsPerRun = 5;
        var apiKeyHeader = "X-Api-Key";
        var apiKey = Environment.GetEnvironmentVariable("SUBIEKT_NEXO_CONNECTOR_API_KEY");

        for (var index = 0; index < args.Length; index++)
        {
            string NextValue()
            {
                if (++index >= args.Length)
                    throw new ArgumentException($"Brak wartości dla {args[index - 1]}.");
                return args[index];
            }

            switch (args[index])
            {
                case "--base-uri":
                case "--uri": baseUri = new Uri(NextValue(), UriKind.Absolute); break;
                case "--concurrency": concurrency = NextValue().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse).ToList(); break;
                case "--repetitions": repetitions = int.Parse(NextValue()); break;
                case "--timeout-seconds": timeoutSeconds = int.Parse(NextValue()); break;
                case "--products-page-size": productsPageSize = int.Parse(NextValue()); break;
                case "--details-per-run": detailsPerRun = int.Parse(NextValue()); break;
                case "--api-key-header": apiKeyHeader = NextValue(); break;
                case "--api-key": apiKey = NextValue(); break;
                case "--help":
                    Console.WriteLine("Użycie: dotnet run --project tools/SferaConcurrencyProbe -- [--base-uri URL] [--concurrency 4,8,16] [--repetitions N] [--details-per-run N] [--products-page-size N] [--timeout-seconds N] [--api-key-header Nazwa] [--api-key Klucz]");
                    Environment.Exit(0);
                    break;
                default: throw new ArgumentException($"Nieznany parametr: {args[index]}");
            }
        }

        if (concurrency.Count == 0 || concurrency.Any(value => value <= 0) || repetitions <= 0 || timeoutSeconds <= 0 || productsPageSize <= 0 || detailsPerRun <= 0)
            throw new ArgumentException("Poziomy równoległości, liczba powtórzeń, timeout, page size i liczba detali muszą być większe od zera.");

        return new ProbeOptions(baseUri, concurrency, repetitions, timeoutSeconds, productsPageSize, detailsPerRun, apiKeyHeader, apiKey);
    }
}
