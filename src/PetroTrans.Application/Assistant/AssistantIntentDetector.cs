using System.Text;

namespace PetroTrans.Application.Assistant;

public static class AssistantIntentDetector
{
    public static string Detect(string text)
    {
        var n = Normalize(text);
        if (ContainsAny(n, "اطبع كشف", "طباعه كشف", "طباعة كشف")) return AssistantIntentTypes.PrintStatement;
        if (ContainsAny(n, "اطبع فاتور", "طباعه فاتور", "طباعة فاتور")) return AssistantIntentTypes.PrintInvoice;
        if (ContainsAny(n, "اطبع ايصال", "اطبع إيصال", "طباعة إيصال", "اطبع التحصيل")) return AssistantIntentTypes.PrintPayment;
        if (ContainsAny(n, "اطبع تقرير", "طباعة تقرير", "اطبع المبيعات")) return AssistantIntentTypes.PrintReport;
        if (ContainsAny(n, "كشف حساب", "كشف الحساب")) return AssistantIntentTypes.CustomerStatement;
        if (ContainsAny(n, "مديونيه", "مديونية", "رصيد العميل", "عليه كام", "عليها كام", "كم عليه", "كام عليه")) return AssistantIntentTypes.CustomerBalance;
        if (ContainsAny(n, "فواتير العميل", "فواتير ل", "عرض فواتير", "وريني فواتير", "هات فواتير")) return AssistantIntentTypes.CustomerInvoices;
        if (ContainsAny(n, "المدينين", "اللي عليهم", "عليهم فلوس", "قائمة المديون", "مين عليه")) return AssistantIntentTypes.ListOwing;
        if (ContainsAny(n, "تقرير مبيعات", "مبيعات اليوم", "تقرير البيع", "مبيعات شهر", "هات مبيعات", "اعرض مبيعات", "مبيعات اغسطس", "مبيعات أغسطس")) return AssistantIntentTypes.SalesReport;
        if (ContainsAny(n, "ارشف العميل", "أرشف العميل", "ارشفة عميل", "أرشفة عميل", "احذف العميل", "امسح العميل")) return AssistantIntentTypes.ArchiveCustomer;
        if (ContainsAny(n, "ناقص مخزون", "تحت الحد", "مخزون منخفض", "مخزون قليل", "اللي ناقص")) return AssistantIntentTypes.InventoryLow;
        if (ContainsAny(n, "كام مخزون", "كم المخزون", "المخزن", "المخزون")) return AssistantIntentTypes.InventoryLookup;
        if (ContainsAny(n, "عميل جديد", "اضف عميل", "أضف عميل", "انشئ عميل", "أنشئ عميل", "ضيف عميل")) return AssistantIntentTypes.CreateCustomer;
        if (ContainsAny(n, "سعر خاص", "سعر للعميل")) return AssistantIntentTypes.UpdateCustomerPrice;
        if (ContainsAny(n, "عدل السعر", "عدّل السعر", "غير السعر", "غيّر السعر", "عدل سعر", "عدّل سعر", "غير سعر")) return AssistantIntentTypes.UpdateBasePrice;
        if (ContainsAny(n, "اسعار", "أسعار", "هات سعر", "سعر فوايج", "سعر الشركة")) return AssistantIntentTypes.GetPrices;
        if (ContainsAny(n, "تحصيل", "دفعه", "دفعة", "سداد", "قبض", "ايداع", "إيداع", "دفعت")) return AssistantIntentTypes.RecordPayment;
        if (ContainsAny(n, "فاتور", "كرتون", "كراتين")) return AssistantIntentTypes.SalesInvoice;
        return AssistantIntentTypes.Unknown;
    }

    public static bool ContainsAny(string haystack, params string[] needles)
        => needles.Any(n => haystack.Contains(Normalize(n), StringComparison.Ordinal));

    public static string Normalize(string value)
    {
        var trimmed = (value ?? string.Empty).Trim().ToLowerInvariant();
        var builder = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed.Normalize(NormalizationForm.FormKC))
        {
            builder.Append(ch switch
            {
                'أ' or 'إ' or 'آ' => 'ا',
                'ة' => 'ه',
                'ى' => 'ي',
                _ => ch
            });
        }

        return builder.ToString();
    }

    public static (DateTime? From, DateTime? To) DetectDateRange(string text, DateTime today)
    {
        var n = Normalize(text);
        var year = today.Year;
        if (ContainsAny(n, "اغسطس", "أغسطس"))
        {
            return (new DateTime(year, 8, 1), new DateTime(year, 8, 31));
        }

        if (ContainsAny(n, "يوليو", "يوليه"))
        {
            return (new DateTime(year, 7, 1), new DateTime(year, 7, 31));
        }

        if (ContainsAny(n, "اول الشهر", "أول الشهر", "من اول الشهر"))
        {
            return (new DateTime(year, today.Month, 1), today.Date);
        }

        if (ContainsAny(n, "اليوم"))
        {
            return (today.Date, today.Date);
        }

        return (null, null);
    }
}
