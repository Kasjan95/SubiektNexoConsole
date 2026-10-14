using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class PartyAdvancedFieldEntityConfiguration : IEntityTypeConfiguration<PartyAdvancedFieldEntity>
{
    public void Configure(EntityTypeBuilder<PartyAdvancedFieldEntity> builder)
    {
        builder.ToTable("PartyAdvancedFields");
        builder.HasKey(field => new { field.PartyId, field.FieldId });
        builder.Property(field => field.FieldId).IsRequired();
        builder.Property(field => field.DataType).HasConversion<string>().IsRequired();
        builder.HasOne<PartyEntity>()
            .WithMany(party => party.AdvancedFields)
            .HasForeignKey(field => field.PartyId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(field => field.FieldId);
    }
}
