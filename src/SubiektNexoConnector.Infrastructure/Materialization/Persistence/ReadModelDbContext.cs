using Microsoft.EntityFrameworkCore;

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class ReadModelDbContext : DbContext
{
    public ReadModelDbContext(DbContextOptions<ReadModelDbContext> options)
        : base(options)
    {
    }

    public DbSet<ProductEntity> Products => Set<ProductEntity>();
    public DbSet<ProductPriceEntity> ProductPrices => Set<ProductPriceEntity>();
    public DbSet<ProductStockEntity> ProductStocks => Set<ProductStockEntity>();
    public DbSet<ProductSupplierEntity> ProductSuppliers => Set<ProductSupplierEntity>();
    public DbSet<ProductBasicFieldsEntity> ProductBasicFields => Set<ProductBasicFieldsEntity>();
    public DbSet<ProductAdvancedFieldEntity> ProductAdvancedFields => Set<ProductAdvancedFieldEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ProductEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ProductPriceEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ProductStockEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ProductSupplierEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ProductBasicFieldsEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ProductAdvancedFieldEntityConfiguration());
    }
}
