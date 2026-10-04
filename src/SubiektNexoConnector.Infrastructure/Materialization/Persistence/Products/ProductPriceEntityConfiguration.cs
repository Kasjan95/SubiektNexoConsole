using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class ProductPriceEntityConfiguration
    : IEntityTypeConfiguration<ProductPriceEntity>
{
    public void Configure(EntityTypeBuilder<ProductPriceEntity> builder)
    {
        builder.ToTable("ProductPrices");
        builder.HasKey(price => new
        {
            price.ProductId,
            price.PriceLevelName
        });
        builder.Property(price => price.PriceLevelName).IsRequired();
        builder.HasOne<ProductEntity>().WithMany().HasForeignKey(price => price.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}