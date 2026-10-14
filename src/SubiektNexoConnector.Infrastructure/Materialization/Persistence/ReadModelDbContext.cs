using Microsoft.EntityFrameworkCore;

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class ReadModelDbContext : DbContext
{
    public ReadModelDbContext(DbContextOptions<ReadModelDbContext> options)
        : base(options)
    {
    }

    public DbSet<ProductEntity> Products => Set<ProductEntity>();
    public DbSet<ProductPriceEntity> ProductPrices => Set<ProductPriceEntity>();
    public DbSet<ProductStockEntity> ProductStocks => Set<ProductStockEntity>();
    public DbSet<ProductSupplierEntity> ProductSuppliers => Set<ProductSupplierEntity>();
    public DbSet<ProductBasicFieldsEntity> ProductBasicFields => Set<ProductBasicFieldsEntity>();
    public DbSet<ProductAdvancedFieldEntity> ProductAdvancedFields => Set<ProductAdvancedFieldEntity>();
    public DbSet<PartyEntity> Parties => Set<PartyEntity>();
    public DbSet<PartyBasicFieldsEntity> PartyBasicFields => Set<PartyBasicFieldsEntity>();
    public DbSet<PartyAdvancedFieldEntity> PartyAdvancedFields => Set<PartyAdvancedFieldEntity>();
    public DbSet<PartyAddressEntity> PartyAddresses => Set<PartyAddressEntity>();
    public DbSet<PartyContactEntity> PartyContacts => Set<PartyContactEntity>();
    public DbSet<PartyIndustryEntity> PartyIndustries => Set<PartyIndustryEntity>();
    public DbSet<PartyFeatureEntity> PartyFeatures => Set<PartyFeatureEntity>();
    public DbSet<MaterializationStateEntity> MaterializationStates => Set<MaterializationStateEntity>();
    public DbSet<MaterializationPolicyEntity> MaterializationPolicies => Set<MaterializationPolicyEntity>();
    public DbSet<AdvancedFieldGroupEntity> AdvancedFieldGroups => Set<AdvancedFieldGroupEntity>();
    public DbSet<AdvancedFieldDefinitionEntity> AdvancedFieldDefinitions => Set<AdvancedFieldDefinitionEntity>();
    public DbSet<AdvancedFieldDictionaryOptionEntity> AdvancedFieldDictionaryOptions => Set<AdvancedFieldDictionaryOptionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ProductEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ProductPriceEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ProductStockEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ProductSupplierEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ProductBasicFieldsEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ProductAdvancedFieldEntityConfiguration());
        modelBuilder.ApplyConfiguration(new PartyEntityConfiguration());
        modelBuilder.ApplyConfiguration(new PartyBasicFieldsEntityConfiguration());
        modelBuilder.ApplyConfiguration(new PartyAdvancedFieldEntityConfiguration());
        modelBuilder.ApplyConfiguration(new PartyAddressEntityConfiguration());
        modelBuilder.ApplyConfiguration(new PartyContactEntityConfiguration());
        modelBuilder.ApplyConfiguration(new PartyIndustryEntityConfiguration());
        modelBuilder.ApplyConfiguration(new PartyFeatureEntityConfiguration());
        modelBuilder.ApplyConfiguration(new MaterializationStateEntityConfiguration());
        modelBuilder.ApplyConfiguration(new MaterializationPolicyEntityConfiguration());

        modelBuilder.ApplyConfiguration(new AdvancedFieldGroupEntityConfiguration());
        modelBuilder.ApplyConfiguration(new AdvancedFieldDefinitionEntityConfiguration());
        modelBuilder.ApplyConfiguration(new AdvancedFieldDictionaryOptionEntityConfiguration());
    }
}
