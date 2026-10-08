namespace PetroTrans.Domain.Catalog;

public static class CatalogDisplayOrder
{
    public static int CategoryRank(string? category)
    {
        var key = Normalize(category);
        if (key.Contains("بنزين", StringComparison.Ordinal))
        {
            return 1;
        }

        if (key.Contains("ديزل", StringComparison.Ordinal))
        {
            return 2;
        }

        if (key.Contains("خاص", StringComparison.Ordinal))
        {
            return 3;
        }

        return 80;
    }

    public static string CategoryTitle(string? category)
    {
        var key = Normalize(category);
        if (key.Contains("بنزين", StringComparison.Ordinal))
        {
            return "زيوت محركات البنزين";
        }

        if (key.Contains("ديزل", StringComparison.Ordinal))
        {
            return "زيوت محركات الديزل";
        }

        if (key.Contains("خاص", StringComparison.Ordinal))
        {
            return "منتجات خاصة";
        }

        return string.IsNullOrWhiteSpace(category) ? "أصناف أخرى" : category.Trim();
    }

    public static int ProductRank(string? name, string? brand = null, string? specification = null)
    {
        var hay = Normalize($"{name} {brand} {specification}");
        if (Contains(hay, "جولد") && (hay.Contains("5w40") || hay.Contains("5w-40")))
        {
            return 10;
        }

        if (Contains(hay, "جولد") && (hay.Contains("5w30") || hay.Contains("5w-30")))
        {
            return 20;
        }

        if (Contains(hay, "برونز"))
        {
            return 30;
        }

        if (hay.Contains("sg", StringComparison.Ordinal))
        {
            return 40;
        }

        if (Contains(hay, "اكسترا") || (hay.Contains("sf", StringComparison.Ordinal) && hay.Contains("50", StringComparison.Ordinal)))
        {
            return 50;
        }

        if (Contains(hay, "الترا") || hay.Contains("ci4", StringComparison.Ordinal) || hay.Contains("ci 4", StringComparison.Ordinal))
        {
            return 60;
        }

        if (hay.Contains("15w40") || hay.Contains("15w-40") || (Contains(hay, "بلس") && hay.Contains("hpsd", StringComparison.Ordinal)))
        {
            return 70;
        }

        if (hay.Contains("hpsd", StringComparison.Ordinal) && !hay.Contains("plus", StringComparison.Ordinal) && !Contains(hay, "بلس"))
        {
            return 80;
        }

        if (hay.Contains("hd", StringComparison.Ordinal) && !hay.Contains("hpsd", StringComparison.Ordinal))
        {
            return 90;
        }

        if (Contains(hay, "تبريد") || hay.Contains("coolant", StringComparison.Ordinal))
        {
            return 100;
        }

        if (Contains(hay, "فرامل") || hay.Contains("dot", StringComparison.Ordinal))
        {
            return 110;
        }

        if (Contains(hay, "شحم"))
        {
            return 120;
        }

        if (Contains(hay, "هيدروليك") || hay.Contains("h 68", StringComparison.Ordinal) || hay.Contains("h68", StringComparison.Ordinal))
        {
            return 130;
        }

        return 900;
    }

    public static int PackagingRank(string? size)
    {
        var value = Normalize(size);
        if (value.Contains(".5", StringComparison.Ordinal) || value.Contains("0.5", StringComparison.Ordinal))
        {
            return 10;
        }

        if (value.Contains("16", StringComparison.Ordinal))
        {
            return 60;
        }

        if (value.Contains("15", StringComparison.Ordinal))
        {
            return 50;
        }

        if (value.Contains("20", StringComparison.Ordinal))
        {
            return 70;
        }

        if (value.Contains('1') && !value.Contains("10", StringComparison.Ordinal))
        {
            return 20;
        }

        if (value.Contains('4') && !value.Contains("40", StringComparison.Ordinal))
        {
            return 30;
        }

        if (value.Contains('5') && !value.Contains("50", StringComparison.Ordinal))
        {
            return 40;
        }

        return 80;
    }

    private static bool Contains(string hay, string needle)
        => hay.Contains(needle, StringComparison.Ordinal);

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var text = value.Trim().ToLowerInvariant()
            .Replace('أ', 'ا')
            .Replace('إ', 'ا')
            .Replace('آ', 'ا');
        while (text.Contains("  ", StringComparison.Ordinal))
        {
            text = text.Replace("  ", " ", StringComparison.Ordinal);
        }

        return text;
    }
}
