using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class ReadModelDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<ReadModelDbContext>
{
    public ReadModelDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ReadModelDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        return new ReadModelDbContext(options);
    }
}