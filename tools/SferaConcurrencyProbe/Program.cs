using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;

var options = ProbeOptions.Parse(args);
using var client = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
};

if (!string.IsNullOrWhiteSpace(options.ApiKey))
{
    client.DefaultRequestHeaders.Add(options.ApiKeyHeader, options.ApiKey);
}

Console.WriteLine($"GET {options.Uri}");
Console.WriteLine($"Poziomy równoległości: {string.Join(", ", options.Concurrency)}; powtórzenia: {options.Repetitions}.");

var allBatches = new List<BatchResult>();
foreach (var concurrency in options.Concurrency)
{
    for (var iteration = 1; iteration <= options.Repetitions; iteration++)
    {
        var batch = await RunBatchAsync(client, options.Uri, concurrency, iteration);
        allBatches.Add(batch);

        Console.WriteLine(
            $"n={concurrency,2}, próba={iteration,2}: {batch.SuccessCount}/{concurrency} 2xx, " +
            $"całość={batch.Elapsed.TotalMilliseconds,8:N0} ms, " +
            $"min/p50/max={batch.MinMilliseconds:N0}/{batch.P50Milliseconds:N0}/{batch.MaxMilliseconds:N0} ms, " +
            $"statusy: {batch.StatusSummary}");
    }
}

Console.WriteLine();
Console.WriteLine("Podsumowanie:");
foreach (var concurrency in options.Concurrency)
{
    var batches = allBatches.Where(batch => batch.Concurrency == concurrency).ToList();
    var completedRequests = batches.SelectMany(batch => batch.Requests).ToList();
    var successful = completedRequests.Count(request => request.IsSuccessStatusCode);
    var elapsed = batches.Select(batch => batch.Elapsed.TotalMilliseconds).Order().ToList();

    Console.WriteLine(
        $"n={concurrency,2}: {successful}/{completedRequests.Count} 2xx, " +
        $"czas serii min/p50/max={elapsed.First():N0}/{Percentile(elapsed, 0.5):N0}/{elapsed.Last():N0} ms");
}

return allBatches.SelectMany(batch => batch.Requests).All(request => request.IsSuccessStatusCode) ? 0 : 1;

static async Task<BatchResult> RunBatchAsync(HttpClient client, Uri uri, int concurrency, int iteration)
{
    var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var requests = Enumerable.Range(1, concurrency)
        .Select(index => SendAsync(client, uri, gate, index))
        .ToList();

    var stopwatch = Stopwatch.StartNew();
    gate.SetResult();
    var results = await Task.WhenAll(requests);
    stopwatch.Stop();

    return new BatchResult(concurrency, iteration, stopwatch.Elapsed, results);
}

static async Task<RequestResult> SendAsync(HttpClient client, Uri uri, TaskCompletionSource gate, int index)
{
    await gate.Task.ConfigureAwait(false);
    var stopwatch = Stopwatch.StartNew();

    try
    {
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
        stopwatch.Stop();
        return new RequestResult(index, response.StatusCode, stopwatch.Elapsed, null);
    }
    catch (Exception exception)
    {
        stopwatch.Stop();
        return new RequestResult(index, null, stopwatch.Elapsed, exception.GetType().Name + ": " + exception.Message);
    }
}

static double Percentile(IReadOnlyList<double> orderedValues, double percentile)
{
    if (orderedValues.Count == 0)
    {
        return 0;
    }

    var position = (orderedValues.Count - 1) * percentile;
    var lower = (int)Math.Floor(position);
    var upper = (int)Math.Ceiling(position);
    return lower == upper
        ? orderedValues[lower]
        : orderedValues[lower] + ((orderedValues[upper] - orderedValues[lower]) * (position - lower));
}

sealed record RequestResult(int Index, HttpStatusCode? StatusCode, TimeSpan Elapsed, string? Error)
{
    public bool IsSuccessStatusCode => StatusCode is >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices;
}

sealed record BatchResult(int Concurrency, int Iteration, TimeSpan Elapsed, IReadOnlyList<RequestResult> Requests)
{
    public int SuccessCount => Requests.Count(request => request.IsSuccessStatusCode);
    public double MinMilliseconds => Requests.Min(request => request.Elapsed.TotalMilliseconds);
    public double P50Milliseconds => CalculatePercentile(Requests.Select(request => request.Elapsed.TotalMilliseconds).Order().ToList(), 0.5);
    public double MaxMilliseconds => Requests.Max(request => request.Elapsed.TotalMilliseconds);
    public string StatusSummary => string.Join(
        ", ",
        Requests.GroupBy(request => request.StatusCode?.ToString() ?? request.Error ?? "unknown")
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Key} ×{group.Count()}"));

    private static double CalculatePercentile(IReadOnlyList<double> orderedValues, double percentile)
    {
        var position = (orderedValues.Count - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        return lower == upper
            ? orderedValues[lower]
            : orderedValues[lower] + ((orderedValues[upper] - orderedValues[lower]) * (position - lower));
    }
}

sealed record ProbeOptions(Uri Uri, IReadOnlyList<int> Concurrency, int Repetitions, int TimeoutSeconds, string ApiKeyHeader, string? ApiKey)
{
    public static ProbeOptions Parse(string[] args)
    {
        var uri = new Uri("https://localhost:7214/warehouses");
        var concurrency = new List<int> { 2, 4, 8 };
        var repetitions = 1;
        var timeoutSeconds = 60;
        var apiKeyHeader = "X-Api-Key";
        var apiKey = Environment.GetEnvironmentVariable("SUBIEKT_NEXO_CONNECTOR_API_KEY");

        for (var index = 0; index < args.Length; index++)
        {
            string NextValue()
            {
                if (++index >= args.Length)
                {
                    throw new ArgumentException($"Brak wartości dla {args[index - 1]}.");
                }

                return args[index];
            }

            switch (args[index])
            {
                case "--uri":
                    uri = new Uri(NextValue(), UriKind.Absolute);
                    break;
                case "--concurrency":
                    concurrency = NextValue().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(int.Parse)
                        .ToList();
                    break;
                case "--repetitions":
                    repetitions = int.Parse(NextValue());
                    break;
                case "--timeout-seconds":
                    timeoutSeconds = int.Parse(NextValue());
                    break;
                case "--api-key-header":
                    apiKeyHeader = NextValue();
                    break;
                case "--api-key":
                    apiKey = NextValue();
                    break;
                case "--help":
                    Console.WriteLine("Użycie: dotnet run --project tools/SferaConcurrencyProbe -- [--uri URL] [--concurrency 2,4,8] [--repetitions N] [--timeout-seconds N] [--api-key-header Nazwa] [--api-key Klucz]");
                    Environment.Exit(0);
                    break;
                default:
                    throw new ArgumentException($"Nieznany parametr: {args[index]}");
            }
        }

        if (concurrency.Count == 0 || concurrency.Any(value => value <= 0) || repetitions <= 0 || timeoutSeconds <= 0)
        {
            throw new ArgumentException("Poziomy równoległości, liczba powtórzeń i timeout muszą być większe od zera.");
        }

        return new ProbeOptions(uri, concurrency, repetitions, timeoutSeconds, apiKeyHeader, apiKey);
    }
}
