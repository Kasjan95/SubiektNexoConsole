using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class AdvancedFieldDefinitionEntityConfiguration : IEntityTypeConfiguration<AdvancedFieldDefinitionEntity>
{
    public void Configure(EntityTypeBuilder<AdvancedFieldDefinitionEntity> builder)
    {
        builder.ToTable("AdvancedFieldDefinitions");
        builder.HasKey(field => field.Id);
        builder.Property(field => field.Id).ValueGeneratedOnAdd();

        builder.HasIndex(field => new { field.Target, field.FieldId }).IsUnique();

        builder.Property(field => field.Target).HasConversion<string>().IsRequired();
        builder.Property(field => field.FieldId).IsRequired();
        builder.Property(field => field.Name).IsRequired();
        builder.Property(field => field.DataType).HasConversion<string>().IsRequired();
        builder.Property(field => field.DictionaryKind).HasConversion<string>();

        builder.HasOne(field => field.Group).WithMany().HasForeignKey(field => field.GroupId).OnDelete(DeleteBehavior.SetNull);
    }
}
