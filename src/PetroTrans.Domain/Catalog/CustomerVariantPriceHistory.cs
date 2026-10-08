namespace PetroTrans.Domain.Catalog;

public sealed class CustomerVariantPriceHistory
{
    public Guid Id { get; set; }
    public Guid? CustomerVariantPriceId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid VariantId { get; set; }
    public decimal? OldUnitPrice { get; set; }
    public decimal? NewUnitPrice { get; set; }
    public string ChangeKind { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; }
    public Guid? ChangedByUserId { get; set; }
}

public static class PriceChangeKinds
{
    public const string Set = "set";
    public const string Update = "update";
    public const string Remove = "remove";
}
