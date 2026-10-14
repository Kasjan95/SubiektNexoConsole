using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class PartyContactEntityConfiguration : IEntityTypeConfiguration<PartyContactEntity>
{
    public void Configure(EntityTypeBuilder<PartyContactEntity> builder)
    {
        builder.ToTable("PartyContacts");
        builder.HasKey(contact => new { contact.PartyId, contact.ContactId });
        builder.HasOne<PartyEntity>()
            .WithMany(party => party.Contacts)
            .HasForeignKey(contact => contact.PartyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
