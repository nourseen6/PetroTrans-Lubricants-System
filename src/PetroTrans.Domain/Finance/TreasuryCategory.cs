using PetroTrans.Domain;

namespace PetroTrans.Domain.Finance;

/// <summary>
/// A treasury item the owner can rename, add, or hide from Settings.
/// Entries keep the <see cref="Code"/>, so renaming never changes old movements.
/// </summary>
public sealed class TreasuryCategory : IAuditedEntity
{
    public const string BothDirections = "both";
    /// <summary>Used only in the Banque Misr book, never in the cash-box form.</summary>
    public const string BankOnly = "bank";

    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>"in", "out", "both", or "bank" — which cash-book form lists it ("bank" is in none).</summary>
    public string Direction { get; set; } = TreasuryDirections.Out;
    public bool IsActive { get; set; } = true;
    /// <summary>Built-in items the system posts to by itself; they can be renamed but not hidden.</summary>
    public bool IsSystem { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;

    public bool Allows(string direction) => Direction == BothDirections || Direction == direction;
}

public static class TreasuryCategoryDefaults
{
    public sealed record Default(string Code, string Name, string Direction, bool IsSystem, bool IsActive = true);

    // Existing codes keep their meaning so old movements still match; the names are what the owner sees.
    public static readonly IReadOnlyList<Default> All =
    [
        new(TreasuryCategories.Sales, "تحصيل من عميل", TreasuryDirections.In, true),
        new("cash_sales", "مبيعات نقدي", TreasuryDirections.In, false),
        // Old data has deposits to more than one bank (QNB too), so these names stay bank-neutral.
        new(TreasuryCategories.BankWithdrawal, "سحب من البنك", TreasuryDirections.In, true),
        new("advance_return", "استرداد سلفة", TreasuryDirections.In, false),
        new(TreasuryCategories.Salaries, "مرتبات", TreasuryDirections.Out, true),
        new(TreasuryCategories.Rent, "إيجار", TreasuryDirections.Out, false),
        new("utilities", "كهرباء ومياه", TreasuryDirections.Out, false),
        new("phone_internet", "تليفون وإنترنت", TreasuryDirections.Out, false),
        new(TreasuryCategories.OfficeExpense, "مصاريف مكتب", TreasuryDirections.Out, false),
        new(TreasuryCategories.OfficePurchases, "مشتريات مكتب", TreasuryDirections.Out, false),
        new(TreasuryCategories.WarehouseExpense, "مصاريف المخزن", TreasuryDirections.Out, false),
        new(TreasuryCategories.CarExpense, "مصاريف عربيات (بنزين/سولار، صيانة)", TreasuryDirections.Out, false),
        new("shipping", "نقل وشحن", TreasuryDirections.Out, false),
        new(TreasuryCategories.SupplierPayment, "سداد مورد", TreasuryDirections.Out, true),
        new(TreasuryCategories.BankDeposit, "إيداع في البنك", TreasuryDirections.Out, true),
        new("taxes_fees", "ضرايب ورسوم", TreasuryDirections.Out, false),
        new("employee_advance", "سلفة لموظف", TreasuryDirections.Out, false),
        new(TreasuryCategories.Withdrawal, "مسحوبات شخصية", TreasuryDirections.Out, false),
        new(TreasuryCategories.BankFees, "مصاريف بنكية", TreasuryCategory.BankOnly, true),
        new(TreasuryCategories.Other, "غير ذلك", TreasuryCategory.BothDirections, true),
        // Old "deposit" rows from before Banque Misr existed; hidden from forms, kept for their label.
        new(TreasuryCategories.Deposit, "إيداع بنك (قديم)", TreasuryDirections.In, true, IsActive: false),
    ];
}
