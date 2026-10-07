using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class MaterializationStateEntityConfiguration
    : IEntityTypeConfiguration<MaterializationStateEntity>
{
    public void Configure(EntityTypeBuilder<MaterializationStateEntity> builder)
    {
        builder.ToTable("MaterializationStates");
        builder.HasKey(state => state.PolicyKey);
        builder.Property(state => state.PolicyKey).HasMaxLength(100);
        builder.Property(state => state.LastError).HasMaxLength(2000);
    }
}