using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class ProductSupplierEntityConfiguration : IEntityTypeConfiguration<ProductSupplierEntity>
{
    public void Configure(EntityTypeBuilder<ProductSupplierEntity> builder)
    {
        builder.ToTable("ProductSuppliers");
        builder.HasKey(supplier => new
        {
            supplier.ProductId,
            supplier.SupplierId
        });
        builder.Property(supplier => supplier.Name).IsRequired();
        builder.HasOne<ProductEntity>()
            .WithMany(product => product.Suppliers)
            .HasForeignKey(supplier => supplier.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(supplier => supplier.SupplierId);
    }
}
