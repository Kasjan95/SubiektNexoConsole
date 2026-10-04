using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class ProductAdvancedFieldEntityConfiguration
    : IEntityTypeConfiguration<ProductAdvancedFieldEntity>
{
    public void Configure(EntityTypeBuilder<ProductAdvancedFieldEntity> builder)
    {
        builder.ToTable("ProductAdvancedFields");
        builder.HasKey(field => new { field.ProductId, field.FieldId });
        builder.Property(field => field.FieldId).IsRequired();
        builder.Property(field => field.DataType).HasConversion<string>().IsRequired();
        builder.HasOne<ProductEntity>()
            .WithMany()
            .HasForeignKey(field => field.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(field => field.FieldId);
    }
}
