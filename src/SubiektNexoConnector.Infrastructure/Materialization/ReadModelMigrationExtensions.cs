using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SubiektNexoConnector.Infrastructure.Materialization.Persistence;

namespace SubiektNexoConnector.Infrastructure.Materialization;

public static class ReadModelMigrationExtensions
{
    public static async Task MigrateReadModelAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReadModelDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
