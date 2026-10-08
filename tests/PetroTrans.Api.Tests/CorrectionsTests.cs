using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PetroTrans.Api.Tests;

public class CorrectionsTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Treasury_IncomingMinusOutgoing_KeepsRunningBalance()
    {
        await using var host = await TestHost.StartAsync();
        await host.Client.PostAsJsonAsync("/api/auth/setup", TestHost.SampleSetup());
        await host.Client.PostAsync("/api/auth/logout", null);
        var login = await host.Client.PostAsJsonAsync("/api/auth/login", new { userName = "father", password = "father1" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var incoming = await host.Client.PostAsJsonAsync("/api/treasury", new
        {
            occurredOn = "2026-08-12",
            direction = "in",
            category = "sales",
            description = "مبيعات",
            amount = 100000m,
            notes = (string?)null
        });
        Assert.Equal(HttpStatusCode.Created, incoming.StatusCode);

        var outgoing = await host.Client.PostAsJsonAsync("/api/treasury", new
        {
            occurredOn = "2026-08-12",
            direction = "out",
            category = "salaries",
            description = "رواتب",
            amount = 31000m,
            notes = (string?)null
        });
        Assert.Equal(HttpStatusCode.Created, outgoing.StatusCode);

        var book = await host.Client.GetFromJsonAsync<TreasuryBookResponse>("/api/treasury", Json);
        Assert.NotNull(book);
        Assert.Equal(100000m, book.TotalIn);
        Assert.Equal(31000m, book.TotalOut);
        Assert.Equal(69000m, book.Balance);
        Assert.Equal(2, book.Entries.Length);
        Assert.Equal(100000m, book.Entries[0].RunningBalance);
        Assert.Equal(69000m, book.Entries[1].RunningBalance);
    }

    [Fact]
    public async Task Treasury_FilterByCategory_ReturnsOnlyThatItemAndItsTotal()
    {
        await using var host = await TestHost.StartAsync();
        await host.Client.PostAsJsonAsync("/api/auth/setup", TestHost.SampleSetup());
        await host.Client.PostAsync("/api/auth/logout", null);
        var login = await host.Client.PostAsJsonAsync("/api/auth/login", new { userName = "father", password = "father1" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await host.Client.PostAsJsonAsync("/api/treasury", new
        {
            occurredOn = "2026-08-12",
            direction = "in",
            category = "sales",
            description = "مبيعات",
            amount = 100000m,
            notes = (string?)null
        })).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await host.Client.PostAsJsonAsync("/api/treasury", new
        {
            occurredOn = "2026-08-12",
            direction = "out",
            category = "car_expense",
            description = "بنزين",
            amount = 800m,
            notes = (string?)null
        })).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await host.Client.PostAsJsonAsync("/api/treasury", new
        {
            occurredOn = "2026-08-13",
            direction = "out",
            category = "car_expense",
            description = "صيانة",
            amount = 200m,
            notes = (string?)null
        })).StatusCode);

        var book = await host.Client.GetFromJsonAsync<TreasuryBookResponse>("/api/treasury?category=car_expense", Json);
        Assert.NotNull(book);
        Assert.Equal(2, book.Entries.Length);
        Assert.All(book.Entries, row => Assert.Equal("car_expense", row.Category));
        Assert.Equal(1000m, book.CategoryTotal);
        Assert.Equal(1000m, book.Balance);
        Assert.Equal(0m, book.TotalIn);
        Assert.Equal(1000m, book.TotalOut);
    }

    [Fact]
    public async Task Treasury_BankDeposit_AllowedIncomingAndFilterIncludesOldDeposit()
    {
        await using var host = await TestHost.StartAsync();
        await host.Client.PostAsJsonAsync("/api/auth/setup", TestHost.SampleSetup());
        await host.Client.PostAsync("/api/auth/logout", null);
        await host.Client.PostAsJsonAsync("/api/auth/login", new { userName = "father", password = "father1" });

        Assert.Equal(HttpStatusCode.Created, (await host.Client.PostAsJsonAsync("/api/treasury", new
        {
            occurredOn = "2026-08-12",
            direction = "in",
            category = "deposit",
            description = "ايداع قديم",
            amount = 500m,
            notes = (string?)null
        })).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await host.Client.PostAsJsonAsync("/api/treasury", new
        {
            occurredOn = "2026-08-12",
            direction = "in",
            category = "bank_deposit",
            description = "تحصيل بنكي",
            amount = 200m,
            notes = (string?)null
        })).StatusCode);

        var book = await host.Client.GetFromJsonAsync<TreasuryBookResponse>("/api/treasury?category=bank_deposit", Json);
        Assert.NotNull(book);
        Assert.Equal(2, book.Entries.Length);
        Assert.Equal(700m, book.CategoryTotal);
    }

    [Fact]
    public async Task Reports_CustomerBalancesAndProfit_ReturnAnalysisShape()
    {
        await using var host = await TestHost.StartAsync();
        await host.Client.PostAsJsonAsync("/api/auth/setup", TestHost.SampleSetup());
        await host.Client.PostAsync("/api/auth/logout", null);
        await host.Client.PostAsJsonAsync("/api/auth/login", new { userName = "father", password = "father1" });

        var balances = await host.Client.GetFromJsonAsync<ReportResponse>("/api/reports/customer-balances", Json);
        Assert.NotNull(balances);
        Assert.Contains("عليه", balances.Columns);
        Assert.Contains("له", balances.Columns);
        Assert.NotNull(balances.Kpis);

        var track = await host.Client.GetFromJsonAsync<ReportResponse>("/api/reports/product-track", Json);
        Assert.NotNull(track);
        Assert.Contains("العبوة", track.Columns);

        var profit = await host.Client.GetFromJsonAsync<ReportResponse>("/api/reports/profit", Json);
        Assert.NotNull(profit);
        Assert.Contains("العبوة", profit.Columns);
        Assert.NotNull(profit.Kpis);
        Assert.Contains(profit.Kpis, x => x.Label.Contains("صافي الربح"));
    }

    [Fact]
    public async Task PurchaseInvoice_Get_ReturnsDocumentLines()
    {
        await using var host = await TestHost.StartAsync();
        await host.Client.PostAsJsonAsync("/api/auth/setup", TestHost.SampleSetup());
        await host.Client.PostAsync("/api/auth/logout", null);
        await host.Client.PostAsJsonAsync("/api/auth/login", new { userName = "father", password = "father1" });

        var product = await host.Client.PostAsJsonAsync("/api/products", new
        {
            name = "فوايجر",
            brand = "Voyager",
            category = "",
            specification = "",
            isActive = true,
            imageRelativePath = (string?)null,
            variants = new[]
            {
                new
                {
                    packagingType = "جركن",
                    packagingSize = "20L",
                    sku = (string?)null,
                    barcode = (string?)null,
                    minStock = (decimal?)null,
                    standardWholesalePrice = 2770m,
                    isActive = true
                }
            }
        });
        var variantId = (await product.Content.ReadFromJsonAsync<ProductCreated>(Json))!.Variants[0].Id;
        var supplier = await host.Client.PostAsJsonAsync("/api/suppliers", new { code = "ADNOC", name = "ADNOC", phone = "", address = "", isActive = true });
        var supplierId = (await supplier.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;
        var created = await host.Client.PostAsJsonAsync("/api/purchase-invoices", new
        {
            supplierId,
            invoiceDate = "2026-07-01",
            notes = "فاتورة أدنوك",
            lines = new[] { new { variantId, quantity = 2m, unitPrice = 2859m } }
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var bill = await created.Content.ReadFromJsonAsync<BillResponse>(Json);
        var loaded = await host.Client.GetFromJsonAsync<BillResponse>($"/api/purchase-invoices/{bill!.Id}", Json);
        Assert.NotNull(loaded);
        Assert.Equal("ADNOC", loaded.SupplierName);
        Assert.Equal(5718m, loaded.GoodsTotal);
        Assert.Single(loaded.Lines);
        Assert.Equal(2m, loaded.Lines[0].Quantity);
        Assert.Equal(2859m, loaded.Lines[0].UnitPrice);
    }

    [Fact]
    public async Task CustomerPrice_DoesNotChangeCompanyBasePrice()
    {
        await using var host = await TestHost.StartAsync();
        await host.Client.PostAsJsonAsync("/api/auth/setup", TestHost.SampleSetup());
        await host.Client.PostAsync("/api/auth/logout", null);
        await host.Client.PostAsJsonAsync("/api/auth/login", new { userName = "father", password = "father1" });
        var customer = await host.Client.PostAsJsonAsync("/api/customers", new
        {
            name = "نصرة الشافعي",
            customerTypeId = (Guid?)null,
            contactPerson = "",
            phone = "",
            whatsApp = "",
            address = ""
        });
        var customerId = (await customer.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;
        var product = await host.Client.PostAsJsonAsync("/api/products", new
        {
            name = "فوايجر جولد",
            brand = "Voyager",
            category = "",
            specification = "",
            isActive = true,
            imageRelativePath = (string?)null,
            variants = new[]
            {
                new
                {
                    packagingType = "كرتونة",
                    packagingSize = "12X1",
                    sku = (string?)null,
                    barcode = (string?)null,
                    minStock = (decimal?)null,
                    standardWholesalePrice = 5470m,
                    isActive = true
                }
            }
        });
        var variantId = (await product.Content.ReadFromJsonAsync<ProductCreated>(Json))!.Variants[0].Id;
        var purchase = await host.Client.PutAsJsonAsync($"/api/pricing/purchase-price/{variantId}", new
        {
            unitPrice = 5640m,
            reason = "سعر أدنوك"
        });
        Assert.True(purchase.IsSuccessStatusCode);
        var special = await host.Client.PostAsJsonAsync("/api/pricing/customer-prices", new
        {
            customerId,
            variantId,
            unitPrice = 5300m,
            reason = "سعر عميل"
        });
        Assert.True(special.IsSuccessStatusCode);
        var matrix = await host.Client.GetFromJsonAsync<PricingRow[]>($"/api/pricing/matrix?q=فوايجر", Json);
        var row = Assert.Single(matrix!, x => x.VariantId == variantId);
        Assert.Equal(5470m, row.BasePrice);
        Assert.Equal(5640m, row.PurchasePrice);
        Assert.Equal(1, row.CustomerPriceCount);
    }

    private sealed record IdResponse(Guid Id);
    private sealed record ProductCreated(VariantCreated[] Variants);
    private sealed record VariantCreated(Guid Id);
    private sealed record BillResponse(Guid Id, string SupplierName, decimal GoodsTotal, BillLine[] Lines);
    private sealed record BillLine(decimal Quantity, decimal UnitPrice);
    private sealed record PricingRow(Guid VariantId, decimal? BasePrice, decimal? PurchasePrice, int CustomerPriceCount);
    private sealed record TreasuryBookResponse(decimal TotalIn, decimal TotalOut, decimal Balance, TreasuryRow[] Entries, decimal CategoryTotal = 0);
    private sealed record TreasuryRow(decimal RunningBalance, string? Category = null);
    private sealed record ReportResponse(string[] Columns, ReportKpi[]? Kpis);
    private sealed record ReportKpi(string Label, string Value);
}
