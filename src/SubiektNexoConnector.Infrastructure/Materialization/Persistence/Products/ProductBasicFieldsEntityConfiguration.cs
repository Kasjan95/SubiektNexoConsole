using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class ProductBasicFieldsEntityConfiguration : IEntityTypeConfiguration<ProductBasicFieldsEntity>
{
    public void Configure(EntityTypeBuilder<ProductBasicFieldsEntity> builder)
    {
        builder.ToTable("ProductBasicFields");
        builder.HasKey(fields => fields.ProductId);
        builder.HasOne<ProductEntity>()
            .WithOne()
            .HasForeignKey<ProductBasicFieldsEntity>(fields => fields.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
