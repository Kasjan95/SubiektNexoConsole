using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class MaterializationPolicyEntityConfiguration
    : IEntityTypeConfiguration<MaterializationPolicyEntity>
{
    public void Configure(EntityTypeBuilder<MaterializationPolicyEntity> builder)
    {
        builder.ToTable("MaterializationPolicies");
        builder.HasKey(policy => policy.PolicyKey);
        builder.Property(policy => policy.PolicyKey).HasMaxLength(100);
        builder.Property(policy => policy.LifetimeSeconds).IsRequired();
        builder.Property(policy => policy.UpdatedAtUtc).IsRequired();
    }
}