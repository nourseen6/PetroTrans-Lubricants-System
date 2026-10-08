using PetroTrans.Domain.Catalog;
using Xunit;

namespace PetroTrans.Api.Tests;

public class CatalogDisplayOrderTests
{
    [Fact]
    public void PaperSheet_OrdersCategoriesThenProducts()
    {
        var products = new[]
        {
            ("اكسترا بلس SF  50", "زيوت محركات البنزين"),
            ("فوايجر 20w-50  Api SG", "زيوت محركات البنزين"),
            ("فوايجر برونز 20w-50  Api sl", "زيوت محركات البنزين"),
            ("فوايجر جولد 5w-30  Api SN", "زيوت محركات البنزين"),
            ("فوايجر جولد 5w-40  Api SN", "زيوت محركات البنزين"),
            ("فوايجر 20w-50 HPSD", "زيوت محركات الديزل"),
            ("فوايجر HD  50", "زيوت محركات الديزل"),
            ("فوايجر الترا 10w-40  API CI 4", "زيوت محركات الديزل"),
            ("فوايجر بلس 15w-40 HPSD PLUS", "زيوت محركات الديزل"),
            ("سائل فرامل dot  3", "منتجات خاصه"),
            ("شحم MP", "منتجات خاصه"),
            ("مياه تبريد coolant    33 %", "منتجات خاصه"),
            ("هيدروليك H  68", "منتجات خاصه"),
        };

        var ordered = products
            .OrderBy(x => CatalogDisplayOrder.CategoryRank(x.Item2))
            .ThenBy(x => CatalogDisplayOrder.ProductRank(x.Item1))
            .Select(x => x.Item1)
            .ToArray();

        Assert.Equal(
            [
                "فوايجر جولد 5w-40  Api SN",
                "فوايجر جولد 5w-30  Api SN",
                "فوايجر برونز 20w-50  Api sl",
                "فوايجر 20w-50  Api SG",
                "اكسترا بلس SF  50",
                "فوايجر الترا 10w-40  API CI 4",
                "فوايجر بلس 15w-40 HPSD PLUS",
                "فوايجر 20w-50 HPSD",
                "فوايجر HD  50",
                "مياه تبريد coolant    33 %",
                "سائل فرامل dot  3",
                "شحم MP",
                "هيدروليك H  68"
            ],
            ordered);
    }
}
