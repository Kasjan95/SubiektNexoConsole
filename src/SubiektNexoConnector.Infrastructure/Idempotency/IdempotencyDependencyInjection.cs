using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SubiektNexoConnector.Infrastructure.Idempotency.Persistence;

namespace SubiektNexoConnector.Infrastructure.Idempotency;

public static class IdempotencyDependencyInjection
{
    public static IServiceCollection AddIdempotencyPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var configuredPath = configuration["Idempotency:DatabasePath"];

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException(
                "Missing configuration: Idempotency:DatabasePath.");
        }

        var databasePath = Path.IsPathRooted(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : Path.GetFullPath(configuredPath, AppContext.BaseDirectory);

        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            DefaultTimeout = 2
        }.ToString();

        services.AddDbContextFactory<IdempotencyDbContext>(options =>
            options.UseSqlite(connectionString, sqlite => sqlite.CommandTimeout(2)));
        services.AddSingleton<IdempotencyOperationStore>();
        services.AddScoped<IdempotencyExecutionContext>();
        services.AddSingleton<IdempotencyExecutionAccessor>();

        return services;
    }
}
