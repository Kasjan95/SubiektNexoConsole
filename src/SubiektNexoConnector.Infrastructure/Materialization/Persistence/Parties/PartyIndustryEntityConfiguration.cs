using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class PartyIndustryEntityConfiguration : IEntityTypeConfiguration<PartyIndustryEntity>
{
    public void Configure(EntityTypeBuilder<PartyIndustryEntity> builder)
    {
        builder.ToTable("PartyIndustries");
        builder.HasKey(industry => new { industry.PartyId, industry.Name });
        builder.Property(industry => industry.Name).IsRequired();
        builder.HasOne<PartyEntity>()
            .WithMany(party => party.Industries)
            .HasForeignKey(industry => industry.PartyId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(industry => industry.Name);
    }
}
