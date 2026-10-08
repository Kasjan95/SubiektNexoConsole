namespace SubiektNexoConnector.Core.Application.Products;

public interface IProductFreshnessPolicy
{
    Task<DateTime> GetRefreshDueAtUtcAsync(
        ProductDetailsDto product,
        DateTime refreshedAtUtc,
        CancellationToken cancellationToken = default);
}
