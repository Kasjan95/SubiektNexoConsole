using Microsoft.EntityFrameworkCore;

namespace SubiektNexoConnector.Infrastructure.Idempotency.Persistence;

public sealed class IdempotencyDbContext : DbContext
{
    public const string TableName = "IdempotencyOperations";

    public IdempotencyDbContext(DbContextOptions<IdempotencyDbContext> options)
        : base(options)
    {
    }

    public DbSet<IdempotencyOperationEntity> Operations => Set<IdempotencyOperationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var request = modelBuilder.Entity<IdempotencyOperationEntity>();

        request.ToTable(TableName, table => table.HasCheckConstraint(
            "CK_IdempotencyOperations_State",
            $"State IN ('{IdempotencyRequestState.Pending}', " +
            $"'{IdempotencyRequestState.Executing}', " +
            $"'{IdempotencyRequestState.Completed}', " +
            $"'{IdempotencyRequestState.Indeterminate}')"));

        request.HasKey(item => item.Id);
        request.Property(item => item.Id).ValueGeneratedOnAdd();
        request.Property(item => item.ClientId).HasMaxLength(IdempotencyOperationEntity.MaxClientIdLength).IsRequired();
        request.Property(item => item.Key).HasMaxLength(IdempotencyOperationEntity.MaxKeyLength).IsRequired();
        request.Property(item => item.Method).HasMaxLength(IdempotencyOperationEntity.MaxMethodLength).IsRequired();
        request.Property(item => item.RequestTarget).HasMaxLength(IdempotencyOperationEntity.MaxRequestTargetLength).IsRequired();
        request.Property(item => item.RequestHash).HasMaxLength(IdempotencyOperationEntity.RequestHashLength).IsFixedLength().IsRequired();
        request.Property(item => item.State).HasConversion<string>().HasMaxLength(32).IsRequired();
        request.Property(item => item.ResponseContentType).HasMaxLength(IdempotencyOperationEntity.MaxResponseContentTypeLength);
        request.Property(item => item.ResponseLocation).HasMaxLength(IdempotencyOperationEntity.MaxResponseLocationLength);
        request.Property(item => item.ResponseBody).HasColumnType("BLOB");

        request.HasIndex(item => new { item.ClientId, item.Key })
            .IsUnique()
            .HasDatabaseName("UX_IdempotencyOperations_ClientId_Key");
        request.HasIndex(item => item.ExpiresAtUtc)
            .HasDatabaseName("IX_IdempotencyOperations_ExpiresAtUtc");
    }
}
