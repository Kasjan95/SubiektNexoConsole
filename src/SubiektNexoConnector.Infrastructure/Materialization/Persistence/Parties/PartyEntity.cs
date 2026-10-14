namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class PartyEntity
{
    public int Id { get; set; }

    public required string Signature { get; set; }
    public required string DisplayName { get; set; }
    public bool IsActive { get; set; }

    public short Type { get; set; }
    public byte? Subtype { get; set; }
    public required string TypeName { get; set; }
    public required string SubtypeName { get; set; }

    public int CustomerStatus { get; set; }
    public required string CustomerStatusName { get; set; }

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? CompanyName { get; set; }

    public string? TaxId { get; set; }
    public string? EuTaxId { get; set; }
    public string? BusinessRegistryNumber { get; set; }
    public string? NationalCourtRegisterNumber { get; set; }

    public string? PartyGroup { get; set; }
    public string? Notes { get; set; }

    public int? FlagId { get; set; }
    public string? FlagComment { get; set; }

    public bool TradeCreditEnabled { get; set; }
    public int? PaymentTermDays { get; set; }
    public int? MaximumPaymentDelayDays { get; set; }
    public int? MaximumUnpaidDocumentCount { get; set; }
    public decimal? OverallCreditLimit { get; set; }

    public bool SalesCreditLimitEnabled { get; set; }
    public decimal? SalesMaximumAmount { get; set; }
    public decimal? SalesBelowMaximumAmountPercent { get; set; }
    public decimal? SalesAboveMaximumAmountPercent { get; set; }

    public bool GoodsIssueCreditLimitEnabled { get; set; }
    public decimal? GoodsIssueMaximumAmount { get; set; }
    public decimal? GoodsIssueBelowMaximumAmountPercent { get; set; }
    public decimal? GoodsIssueAboveMaximumAmountPercent { get; set; }

    public bool OrderCreditLimitEnabled { get; set; }
    public decimal? OrderMaximumAmount { get; set; }
    public decimal? OrderBelowMaximumAmountPercent { get; set; }
    public decimal? OrderAboveMaximumAmountPercent { get; set; }

    public DateTime RefreshedAtUtc { get; set; }

    public PartyBasicFieldsEntity? BasicFields { get; set; }
    public ICollection<PartyAdvancedFieldEntity> AdvancedFields { get; set; } = [];
    public ICollection<PartyAddressEntity> Addresses { get; set; } = [];
    public ICollection<PartyContactEntity> Contacts { get; set; } = [];
    public ICollection<PartyIndustryEntity> Industries { get; set; } = [];
    public ICollection<PartyFeatureEntity> Features { get; set; } = [];
}