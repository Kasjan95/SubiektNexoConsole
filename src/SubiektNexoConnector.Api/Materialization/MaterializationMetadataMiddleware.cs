using System.Globalization;
using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Api.Materialization;

public sealed class MaterializationMetadataMiddleware
{
    public const string StatusHeaderName = "Materialization-Status";
    public const string AgeSecondsHeaderName = "Materialization-Age-Seconds";
    public const string LastSuccessAtHeaderName = "Materialization-Last-Success-At";

    private static readonly object HttpContextItemKey = typeof(MaterializationMetadata);
    private readonly RequestDelegate _next;

    public MaterializationMetadataMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            AddHeaders(context);
            return Task.CompletedTask;
        });

        await _next(context);
    }

    internal static void SetMetadata(HttpContext context, MaterializationMetadata metadata) => context.Items[HttpContextItemKey] = metadata;

    private static void AddHeaders(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method))
            return;

        var metadata = GetExplicitMetadata(context);
        if (metadata is null)
        {
            if (context.Response.StatusCode is < StatusCodes.Status200OK or >= StatusCodes.Status300MultipleChoices)
                return;

            metadata = new MaterializationMetadata(MaterializationStatus.Fresh, DateTime.UtcNow);
        }

        context.Response.Headers[StatusHeaderName] = ToHeaderValue(metadata.Status);

        if (metadata.LastSuccessAtUtc is not { } lastSuccessAtUtc)
            return;

        lastSuccessAtUtc = NormalizeUtc(lastSuccessAtUtc);
        var ageSeconds = Math.Max(0, (long)Math.Floor((DateTime.UtcNow - lastSuccessAtUtc).TotalSeconds));
        context.Response.Headers[AgeSecondsHeaderName] = ageSeconds.ToString(CultureInfo.InvariantCulture);
        context.Response.Headers[LastSuccessAtHeaderName] = lastSuccessAtUtc.ToString("O", CultureInfo.InvariantCulture);
    }

    private static MaterializationMetadata? GetExplicitMetadata(HttpContext context) => context.Items.TryGetValue(HttpContextItemKey, out var value) ? value as MaterializationMetadata : null;

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static string ToHeaderValue(MaterializationStatus status) => status switch
    {
        MaterializationStatus.Fresh => "fresh",
        MaterializationStatus.RefreshDue => "refresh-due",
        MaterializationStatus.Stale => "stale",
        MaterializationStatus.Unavailable => "unavailable",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
