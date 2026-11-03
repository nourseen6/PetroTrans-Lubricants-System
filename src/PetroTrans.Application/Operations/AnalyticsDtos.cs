namespace PetroTrans.Application.Operations;

/// <summary>Everything the analysis page shows for one period, computed with the same rules as the profit report.</summary>
public sealed record AnalyticsDto(
    DateTime? From,
    DateTime? To,
    AnalyticsTotalsDto Totals,
    IReadOnlyList<AnalyticsMonthDto> Months,
    IReadOnlyList<AnalyticsProductDto> Products,
    IReadOnlyList<AnalyticsUnsoldDto> Unsold,
    IReadOnlyList<AnalyticsCustomerDto> Customers,
    IReadOnlyList<AnalyticsExpenseDto> Expenses,
    int LinesWithoutCost);

public sealed record AnalyticsTotalsDto(
    decimal Sales,
    decimal Cost,
    decimal GrossProfit,
    decimal Expenses,
    decimal NetProfit,
    int Invoices,
    decimal AverageInvoice,
    int BuyingCustomers,
    decimal Collections,
    decimal Receivables);

public sealed record AnalyticsMonthDto(int Year, int Month, decimal Sales, decimal Cost, decimal Expenses, decimal NetProfit, int Invoices);

public sealed record AnalyticsProductDto(
    Guid VariantId,
    string Name,
    string Pack,
    decimal Quantity,
    decimal Sales,
    int Invoices,
    int Customers,
    decimal? Profit,
    decimal? MarginPercent,
    DateTime LastSold);

/// <summary>An active product with no sale in the period; <see cref="LastSold"/> is null when it was never sold at all.</summary>
public sealed record AnalyticsUnsoldDto(Guid VariantId, string Name, string Pack, decimal OnHand, DateTime? LastSold);

public sealed record AnalyticsCustomerDto(
    Guid CustomerId,
    string Name,
    int Invoices,
    decimal Sales,
    decimal? Profit,
    decimal AverageInvoice,
    DateTime LastPurchase,
    int DaysSinceLastPurchase,
    decimal Outstanding);

public sealed record AnalyticsExpenseDto(string Code, string Name, decimal Amount, int Count);

public interface IAnalyticsService
{
    Task<AnalyticsDto> GetAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
}
