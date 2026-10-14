using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class PartyFeatureEntityConfiguration : IEntityTypeConfiguration<PartyFeatureEntity>
{
    public void Configure(EntityTypeBuilder<PartyFeatureEntity> builder)
    {
        builder.ToTable("PartyFeatures");
        builder.HasKey(feature => new { feature.PartyId, feature.Name });
        builder.Property(feature => feature.Name).IsRequired();
        builder.HasOne<PartyEntity>()
            .WithMany(party => party.Features)
            .HasForeignKey(feature => feature.PartyId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(feature => feature.Name);
    }
}
