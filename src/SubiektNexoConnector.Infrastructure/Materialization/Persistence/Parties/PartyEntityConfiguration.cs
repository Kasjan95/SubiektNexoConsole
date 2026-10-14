using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class PartyEntityConfiguration : IEntityTypeConfiguration<PartyEntity>
{
    public void Configure(EntityTypeBuilder<PartyEntity> builder)
    {
        builder.ToTable("Parties");
        builder.HasKey(party => party.Id);
        builder.Property(party => party.Id).ValueGeneratedNever();
        builder.Property(party => party.Signature).IsRequired();
        builder.Property(party => party.DisplayName).IsRequired();
        builder.Property(party => party.TypeName).IsRequired();
        builder.Property(party => party.SubtypeName).IsRequired();
        builder.Property(party => party.CustomerStatusName).IsRequired();
        builder.HasIndex(party => party.Signature).IsUnique().HasDatabaseName("UX_Parties_Signature");
        builder.HasIndex(party => party.RefreshedAtUtc);
        builder.HasIndex(party => new { party.CustomerStatus, party.Type });
    }
}
