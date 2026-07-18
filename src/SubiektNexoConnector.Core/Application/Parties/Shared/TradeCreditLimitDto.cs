namespace SubiektNexoConnector.Core.Application.Parties.Shared
{
    public sealed record TradeCreditLimitDto(
        bool IsEnabled,
        int? PaymentTermDays,
        int? MaximumPaymentDelayDays,
        int? MaximumUnpaidDocumentCount,
        decimal? OverallLimit,
        DocumentTradeCreditLimitDto Sales,
        DocumentTradeCreditLimitDto GoodsIssue,
        DocumentTradeCreditLimitDto Order
    );

    public sealed record DocumentTradeCreditLimitDto(
        bool IsMaximumAmountEnabled,
        decimal? MaximumAmount,
        decimal? CreditLimitBelowMaximumAmountPercent,
        decimal? CreditLimitAboveMaximumAmountPercent
    );
}
