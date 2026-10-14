using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class PartyBasicFieldsEntityConfiguration : IEntityTypeConfiguration<PartyBasicFieldsEntity>
{
    public void Configure(EntityTypeBuilder<PartyBasicFieldsEntity> builder)
    {
        builder.ToTable("PartyBasicFields");
        builder.HasKey(fields => fields.PartyId);
        builder.HasOne<PartyEntity>()
            .WithOne(party => party.BasicFields)
            .HasForeignKey<PartyBasicFieldsEntity>(fields => fields.PartyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
