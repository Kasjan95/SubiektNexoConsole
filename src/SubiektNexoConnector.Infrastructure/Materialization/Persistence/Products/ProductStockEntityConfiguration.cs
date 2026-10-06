using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class ProductStockEntityConfiguration
    : IEntityTypeConfiguration<ProductStockEntity>
{
    public void Configure(EntityTypeBuilder<ProductStockEntity> builder)
    {
        builder.ToTable("ProductStocks");
        builder.HasKey(stock => new
        {
            stock.ProductId,
            stock.WarehouseSymbol
        });
        builder.Property(stock => stock.WarehouseSymbol).IsRequired();
        builder.HasOne<ProductEntity>()
            .WithMany(product => product.Stocks)
            .HasForeignKey(stock => stock.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
