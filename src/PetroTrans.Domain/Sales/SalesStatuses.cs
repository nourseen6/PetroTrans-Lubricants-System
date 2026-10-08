namespace PetroTrans.Domain.Sales;

public static class SalesStatuses
{
    public const string Draft = "draft";
    public const string Posted = "posted";
}

public static class PaymentStatuses
{
    public const string Unpaid = "unpaid";
    public const string Partial = "partial";
    public const string Paid = "paid";
}

public static class PriceSources
{
    public const string Standard = "standard";
    public const string CustomerType = "customer_type";
    public const string CustomerSpecific = "customer_specific";
    public const string ManualOverride = "manual_override";
}
