using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class AdvancedFieldGroupEntityConfiguration : IEntityTypeConfiguration<AdvancedFieldGroupEntity>
{
    public void Configure(EntityTypeBuilder<AdvancedFieldGroupEntity> builder)
    {
        builder.ToTable("AdvancedFieldGroups");
        builder.HasKey(group => group.Id);
        builder.Property(group => group.Id).ValueGeneratedOnAdd();

        builder.HasIndex(group => new { group.Target, group.Name, group.Position }).IsUnique();

        builder.Property(group => group.Target).HasConversion<string>().IsRequired();
        builder.Property(group => group.Name).IsRequired();
        builder.Property(group => group.Position).IsRequired();
    }
}
