using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SubiektNexoConnector.Infrastructure.Idempotency.Persistence;

namespace SubiektNexoConnector.Api.Idempotency;

public sealed class IdempotencyDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<IdempotencyDbContext>
{
    public IdempotencyDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IdempotencyDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        return new IdempotencyDbContext(options);
    }
}
