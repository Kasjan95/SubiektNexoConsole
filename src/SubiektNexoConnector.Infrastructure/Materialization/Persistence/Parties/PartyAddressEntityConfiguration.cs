using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class PartyAddressEntityConfiguration : IEntityTypeConfiguration<PartyAddressEntity>
{
    public void Configure(EntityTypeBuilder<PartyAddressEntity> builder)
    {
        builder.ToTable("PartyAddresses");
        builder.HasKey(address => new { address.PartyId, address.AddressId });
        builder.HasOne<PartyEntity>()
            .WithMany(party => party.Addresses)
            .HasForeignKey(address => address.PartyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
