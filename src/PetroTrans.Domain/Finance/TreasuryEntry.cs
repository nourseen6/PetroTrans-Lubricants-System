using PetroTrans.Domain;

namespace PetroTrans.Domain.Finance;

public static class TreasuryDirections
{
    public const string In = "in";
    public const string Out = "out";
}

public static class TreasuryCategories
{
    public const string Sales = "sales";
    public const string Salaries = "salaries";
    public const string Rent = "rent";
    public const string OfficeExpense = "office_expense";
    public const string OfficePurchases = "office_purchases";
    public const string WarehouseExpense = "warehouse_expense";
    public const string CarExpense = "car_expense";
    public const string Deposit = "deposit";
    public const string Withdrawal = "withdrawal";
    public const string BankDeposit = "bank_deposit";
    public const string SupplierPayment = "supplier_payment";
    public const string Other = "other";

    public static readonly string[] Incoming = [Sales, Deposit, BankDeposit, Other];
    public static readonly string[] Outgoing =
    [
        Salaries,
        Rent,
        OfficeExpense,
        OfficePurchases,
        WarehouseExpense,
        CarExpense,
        Withdrawal,
        BankDeposit,
        SupplierPayment,
        Other
    ];

    public static bool IsAllowed(string direction, string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return true;
        }

        return direction == TreasuryDirections.In
            ? Incoming.Contains(category)
            : Outgoing.Contains(category);
    }

    public static string? Canonical(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return null;
        }

        return string.Equals(category.Trim(), Deposit, StringComparison.OrdinalIgnoreCase)
            ? BankDeposit
            : category.Trim();
    }

    public static bool MatchesFilter(string? entryCategory, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        if (string.Equals(Canonical(filter), BankDeposit, StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(Canonical(entryCategory), BankDeposit, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(entryCategory, filter, StringComparison.OrdinalIgnoreCase);
    }

    public static bool LooksLikeBank(string? methodName)
    {
        if (string.IsNullOrWhiteSpace(methodName))
        {
            return false;
        }

        var name = methodName.Trim().Replace('أ', 'ا').Replace('إ', 'ا').ToLowerInvariant();
        return name.Contains("بنك", StringComparison.Ordinal)
            || name.Contains("bank", StringComparison.Ordinal)
            || name.Contains("تحويل", StringComparison.Ordinal)
            || name.Contains("insta", StringComparison.Ordinal)
            || name.Contains("فيزا", StringComparison.Ordinal)
            || name.Contains("visa", StringComparison.Ordinal);
    }
}

public sealed class TreasuryEntry : IAuditedEntity
{
    public Guid Id { get; set; }
    public DateTime OccurredOn { get; set; }
    public string Direction { get; set; } = TreasuryDirections.In;
    public string? Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
    public string? SourceDocumentType { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;
}
