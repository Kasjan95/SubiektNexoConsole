
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class AdvancedFieldDictionaryOptionEntityConfiguration : IEntityTypeConfiguration<AdvancedFieldDictionaryOptionEntity>
{
    public void Configure(EntityTypeBuilder<AdvancedFieldDictionaryOptionEntity> builder)
    {
        builder.ToTable("AdvancedFieldDictionaryOptions");

        builder.HasKey(option => new { option.DefinitionId, option.Key });

        builder.Property(option => option.Key).IsRequired();
        builder.Property(option => option.Label).IsRequired();
        builder.Property(option => option.Position).IsRequired();

        builder.HasOne<AdvancedFieldDefinitionEntity>().WithMany(definition => definition.DictionaryOptions).HasForeignKey(option => option.DefinitionId).OnDelete(DeleteBehavior.Cascade);
    }
}