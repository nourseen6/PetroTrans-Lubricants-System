using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetroTrans.Application.Assistant;
using PetroTrans.Domain.Identity;
using PetroTrans.Infrastructure.Persistence;
using Xunit;

namespace PetroTrans.Api.Tests;

public class FinalBusinessTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void AssistantIntentDetector_UnderstandsBusinessPhrases_WithoutGuessingUnknown()
    {
        Assert.Equal(AssistantIntentTypes.CustomerBalance, AssistantIntentDetector.Detect("نصرة عليها كام؟"));
        Assert.Equal(AssistantIntentTypes.CustomerInvoices, AssistantIntentDetector.Detect("وريني فواتير نصرة"));
        Assert.Equal(AssistantIntentTypes.SalesReport, AssistantIntentDetector.Detect("هات مبيعات شهر أغسطس"));
        Assert.Equal(AssistantIntentTypes.ListOwing, AssistantIntentDetector.Detect("مين عليه فلوس أكتر؟"));
        Assert.Equal(AssistantIntentTypes.InventoryLookup, AssistantIntentDetector.Detect("كام مخزون فوايجر برونز 4 لتر؟"));
        Assert.Equal(AssistantIntentTypes.InventoryLow, AssistantIntentDetector.Detect("إيه اللي ناقص في المخزن؟"));
        Assert.Equal(AssistantIntentTypes.GetPrices, AssistantIntentDetector.Detect("هات أسعار فوايجر للعميل فلان"));
        Assert.Equal(AssistantIntentTypes.PrintStatement, AssistantIntentDetector.Detect("اطبع كشف حساب نصره"));
        Assert.Equal(AssistantIntentTypes.UpdateBasePrice, AssistantIntentDetector.Detect("عدّل سعر فوايجر برونز 4 لتر إلى 1200"));
        Assert.Equal(AssistantIntentTypes.CreateCustomer, AssistantIntentDetector.Detect("ضيف عميل جديد اسمه أحمد"));
        Assert.Equal(AssistantIntentTypes.RecordPayment, AssistantIntentDetector.Detect("نصرة دفعت 5000 جنيه"));
        Assert.Equal(AssistantIntentTypes.ArchiveCustomer, AssistantIntentDetector.Detect("احذف العميل"));
        var august = AssistantIntentDetector.DetectDateRange("هات مبيعات شهر أغسطس", new DateTime(2026, 9, 2));
        Assert.Equal(new DateTime(2026, 8, 1), august.From);
        Assert.Equal(new DateTime(2026, 8, 31), august.To);
    }

    [Fact]
    public async Task Customer_ArchiveRestore_HidesFromNormalSelection_KeepsProfile()
    {
        await using var host = await TestHost.StartAsync();
        var api = await SeedAsync(host);
        var archived = await host.Client.PostAsync($"/api/customers/{api.CustomerId}/archive", null);
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);

        var active = await host.Client.GetFromJsonAsync<CustomerListItem[]>($"/api/customers", Json);
        Assert.DoesNotContain(active!, x => x.Id == api.CustomerId);

        var all = await host.Client.GetFromJsonAsync<CustomerListItem[]>("/api/customers?includeInactive=true", Json);
        Assert.Contains(all!, x => x.Id == api.CustomerId && !x.IsActive);

        var profile = await host.Client.GetFromJsonAsync<CustomerResponse>($"/api/customers/{api.CustomerId}", Json);
        Assert.False(profile!.IsActive);

        var restored = await host.Client.PostAsync($"/api/customers/{api.CustomerId}/restore", null);
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        var after = await host.Client.GetFromJsonAsync<CustomerListItem[]>("/api/customers", Json);
        Assert.Contains(after!, x => x.Id == api.CustomerId && x.IsActive);
    }

    [Fact]
    public async Task ProductAndVariant_ArchiveRestore_HiddenFromSalesSearch()
    {
        await using var host = await TestHost.StartAsync();
        var api = await SeedAsync(host);
        var archivedVariant = await host.Client.PostAsync($"/api/variants/{api.VariantId}/archive", null);
        Assert.Equal(HttpStatusCode.OK, archivedVariant.StatusCode);

        var picks = await host.Client.GetFromJsonAsync<VariantPick[]>("/api/variants?q=فوايجر", Json);
        Assert.DoesNotContain(picks!, x => x.Id == api.VariantId);

        var restoredVariant = await host.Client.PostAsync($"/api/variants/{api.VariantId}/restore", null);
        Assert.Equal(HttpStatusCode.OK, restoredVariant.StatusCode);

        var archivedProduct = await host.Client.PostAsync($"/api/products/{api.ProductId}/archive", null);
        Assert.Equal(HttpStatusCode.OK, archivedProduct.StatusCode);
        var products = await host.Client.GetFromJsonAsync<ProductListItem[]>("/api/products", Json);
        Assert.DoesNotContain(products!, x => x.Id == api.ProductId);

        var restoredProduct = await host.Client.PostAsync($"/api/products/{api.ProductId}/restore", null);
        Assert.Equal(HttpStatusCode.OK, restoredProduct.StatusCode);
        var again = await host.Client.GetFromJsonAsync<VariantPick[]>("/api/variants?q=فوايجر", Json);
        Assert.Contains(again!, x => x.Id == api.VariantId);
    }

    [Fact]
    public async Task Pricing_CustomerOverride_DoesNotChangeBase_OrHistoricalInvoiceLines()
    {
        await using var host = await TestHost.StartAsync();
        var api = await SeedAsync(host);

        var invoice = await CreatePostedInvoiceAsync(host, api, 2m, 5000m);
        Assert.Equal(5000m, invoice.Lines[0].UnitPrice);

        var baseUpdate = await host.Client.PutAsJsonAsync($"/api/pricing/base-price/{api.VariantId}", new { unitPrice = 5250m, reason = "قائمة الشركة 2026" });
        Assert.Equal(HttpStatusCode.OK, baseUpdate.StatusCode);

        var special = await host.Client.PostAsJsonAsync("/api/pricing/customer-prices", new
        {
            customerId = api.CustomerId,
            variantId = api.VariantId,
            unitPrice = 5470m,
            reason = "سعر عملاء"
        });
        Assert.True(special.IsSuccessStatusCode);

        var otherCustomer = await host.Client.PostAsJsonAsync("/api/customers", NewCustomer("عميل آخر"));
        var otherId = (await otherCustomer.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;
        var otherPrice = await host.Client.PostAsJsonAsync("/api/pricing/customer-prices", new
        {
            customerId = otherId,
            variantId = api.VariantId,
            unitPrice = 4800m,
            reason = "سعر خاص"
        });
        Assert.True(otherPrice.IsSuccessStatusCode);

        var matrix = await host.Client.GetFromJsonAsync<PricingRow[]>($"/api/pricing/matrix?q=فوايجر", Json);
        var row = Assert.Single(matrix!, x => x.VariantId == api.VariantId);
        Assert.Equal(5250m, row.BasePrice);
        Assert.Equal(2, row.CustomerPriceCount);

        var historical = await host.Client.GetFromJsonAsync<InvoiceResponse>($"/api/sales/invoices/{invoice.Id}", Json);
        Assert.Equal(5000m, historical!.Lines[0].UnitPrice);
        Assert.Equal(10000m, historical.LinesSubtotal);
        Assert.False(string.IsNullOrWhiteSpace(historical.CreatedAt.ToString()));

        var history = await host.Client.GetFromJsonAsync<PriceHistory[]>($"/api/pricing/history?variantId={api.VariantId}", Json);
        Assert.Contains(history!, x => x.NewUnitPrice == 5250m && x.CustomerId == Guid.Empty);
        Assert.Contains(history!, x => x.NewUnitPrice == 5470m && x.CustomerId == api.CustomerId);
    }

    [Fact]
    public async Task Statement_RunningBalance_OpeningClosing_AndDateFilter()
    {
        await using var host = await TestHost.StartAsync();
        var api = await SeedAsync(host);
        var invoice = await CreatePostedInvoiceAsync(host, api, 2m, 1000m, "2026-08-10");
        var pay = await host.Client.PostAsJsonAsync("/api/payments", new
        {
            invoiceId = invoice.Id,
            customerId = api.CustomerId,
            paymentMethodId = api.MethodId,
            amount = 400m,
            paidOn = "2026-09-01",
            reference = (string?)null,
            notes = (string?)null
        });
        Assert.True(pay.IsSuccessStatusCode);
        var payment = await pay.Content.ReadFromJsonAsync<IdResponse>(Json);
        var receipt = await host.Client.GetFromJsonAsync<PaymentResponse>($"/api/payments/{payment!.Id}", Json);
        Assert.Equal(400m, receipt!.Amount);
        Assert.Equal(api.CustomerId, receipt.CustomerId);

        var full = await host.Client.GetFromJsonAsync<StatementResponse>($"/api/customers/{api.CustomerId}/statement", Json);
        Assert.Equal(0m, full!.OpeningBalance);
        Assert.Equal(2000m, full.TotalDebits);
        Assert.Equal(400m, full.TotalCredits);
        Assert.Equal(1600m, full.ClosingBalance);
        Assert.Equal(2, full.Lines.Length);
        Assert.Equal(2000m, full.Lines[0].Debit);
        Assert.Equal(2000m, full.Lines[0].RunningBalance);
        Assert.Equal(400m, full.Lines[1].Credit);
        Assert.Equal(1600m, full.Lines[1].RunningBalance);

        var august = await host.Client.GetFromJsonAsync<StatementResponse>($"/api/customers/{api.CustomerId}/statement?from=2026-08-01&to=2026-08-31", Json);
        Assert.Equal(0m, august!.OpeningBalance);
        Assert.Equal(2000m, august.ClosingBalance);
        Assert.Single(august.Lines);

        var september = await host.Client.GetFromJsonAsync<StatementResponse>($"/api/customers/{api.CustomerId}/statement?from=2026-09-01&to=2026-09-30", Json);
        Assert.Equal(2000m, september!.OpeningBalance);
        Assert.Equal(400m, september.TotalCredits);
        Assert.Equal(1600m, september.ClosingBalance);
    }

    [Fact]
    public async Task Invoice_ManualTotal_Discount_AuditFields_DoNotRewriteLines()
    {
        await using var host = await TestHost.StartAsync();
        var api = await SeedAsync(host);
        var created = await host.Client.PostAsJsonAsync("/api/sales/invoices", new
        {
            customerId = api.CustomerId,
            warehouseId = api.WarehouseId,
            invoiceDate = "2026-08-20",
            dueDate = (string?)null,
            notes = (string?)null,
            discountAmount = 100m,
            manualTotal = 1700m,
            manualTotalReason = "اتفاق خاص",
            lines = new[] { new { variantId = api.VariantId, quantity = 2m, unitPrice = 1000m, overrideReason = (string?)null } }
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var invoice = await created.Content.ReadFromJsonAsync<InvoiceResponse>(Json);
        Assert.True(invoice!.HasManualTotal);
        Assert.Equal(2000m, invoice.LinesSubtotal);
        Assert.Equal(100m, invoice.DiscountAmount);
        Assert.Equal(1700m, invoice.GoodsTotal);
        Assert.Equal(1000m, invoice.Lines[0].UnitPrice);
        Assert.False(string.IsNullOrWhiteSpace(invoice.CreatedByName));
        Assert.NotEqual(default, invoice.CreatedAt);
    }

    [Fact]
    public async Task Reports_HonorDateFilters()
    {
        await using var host = await TestHost.StartAsync();
        var api = await SeedAsync(host);
        await CreatePostedInvoiceAsync(host, api, 1m, 1000m, "2026-08-15");
        await CreatePostedInvoiceAsync(host, api, 1m, 1000m, "2026-09-02");
        var august = await host.Client.GetFromJsonAsync<ReportResponse>("/api/reports/sales?from=2026-08-01&to=2026-08-31", Json);
        Assert.Single(august!.Rows);
        var all = await host.Client.GetFromJsonAsync<ReportResponse>("/api/reports/sales", Json);
        Assert.Equal(2, all!.Rows.Length);
    }

    [Fact]
    public async Task Assistant_ReadWriteApprovalRejectionPermissionAndAmbiguity()
    {
        await using var host = await TestHost.StartAsync();
        var api = await SeedAsync(host);
        await host.Client.PostAsJsonAsync("/api/customers", NewCustomer("محمد نصار"));
        await host.Client.PostAsJsonAsync("/api/customers", NewCustomer("محمد علي"));

        var balance = await host.Client.PostAsJsonAsync("/api/assistant/parse", new { text = "نصرة عليها كام؟", locale = "ar" });
        Assert.Equal(HttpStatusCode.OK, balance.StatusCode);
        var balanceDraft = await balance.Content.ReadFromJsonAsync<AssistantDraftResponse>(Json);
        Assert.Equal(AssistantIntentTypes.CustomerBalance, balanceDraft!.IntentType);
        Assert.False(balanceDraft.CanApprove);

        var invoiceRead = await host.Client.PostAsJsonAsync("/api/assistant/parse", new { text = "وريني فواتير نصرة", locale = "ar" });
        var invoiceDraft = await invoiceRead.Content.ReadFromJsonAsync<AssistantDraftResponse>(Json);
        Assert.Equal(AssistantIntentTypes.CustomerInvoices, invoiceDraft!.IntentType);

        var inventory = await host.Client.PostAsJsonAsync("/api/assistant/parse", new { text = "كام مخزون فوايجر؟", locale = "ar" });
        var inventoryDraft = await inventory.Content.ReadFromJsonAsync<AssistantDraftResponse>(Json);
        Assert.Equal(AssistantIntentTypes.InventoryLookup, inventoryDraft!.IntentType);

        var ambiguous = await host.Client.PostAsJsonAsync("/api/assistant/parse", new { text = "اعمل فاتورة لمحمد", locale = "ar" });
        var amb = await ambiguous.Content.ReadFromJsonAsync<AssistantDraftResponse>(Json);
        Assert.False(amb!.CanApprove);
        Assert.NotEmpty(amb.Ambiguities);

        var payParse = await host.Client.PostAsJsonAsync("/api/assistant/parse", new { text = "تحصيل من نصرة 5000", locale = "ar" });
        var payDraft = await payParse.Content.ReadFromJsonAsync<AssistantDraftResponse>(Json);
        Assert.Equal(AssistantIntentTypes.RecordPayment, payDraft!.IntentType);
        Assert.StartsWith("مسودة تحصيل", payDraft.SummaryAr);
        var rejected = await host.Client.PostAsync($"/api/assistant/drafts/{payDraft.DraftId}/reject", null);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        var rejectedBody = await rejected.Content.ReadFromJsonAsync<AssistantApproveResponse>(Json);
        Assert.Equal("rejected", rejectedBody!.Status);

        var priceParse = await host.Client.PostAsJsonAsync("/api/assistant/parse", new { text = "غير سعر فوايجر إلى 500", locale = "ar" });
        var priceDraft = await priceParse.Content.ReadFromJsonAsync<AssistantDraftResponse>(Json);
        Assert.True(priceDraft!.CanApprove);
        var approved = await host.Client.PostAsync($"/api/assistant/drafts/{priceDraft.DraftId}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        using (var scope = host.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Contains(await db.AuditLogs.Select(x => x.Action).ToListAsync(), x => x == "assistant.approve");
            var perm = await db.Permissions.SingleAsync(x => x.Code == PermissionCodes.PricingEditMasters);
            var role = await db.Roles.SingleAsync(x => x.Code == RoleCodes.Operator);
            var link = await db.RolePermissions.SingleAsync(x => x.RoleId == role.Id && x.PermissionId == perm.Id);
            db.RolePermissions.Remove(link);
            await db.SaveChangesAsync();
        }

        await host.Client.PostAsync("/api/auth/logout", null);
        var login = await host.Client.PostAsJsonAsync("/api/auth/login", new { userName = "me", password = "operator1" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var denied = await host.Client.PostAsJsonAsync("/api/assistant/parse", new { text = "غير سعر فوايجر إلى 900", locale = "ar" });
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
    }

    [Fact]
    public async Task Assistant_CreateInvoiceDraft_RequiresApproval()
    {
        await using var host = await TestHost.StartAsync();
        await SeedAsync(host);
        var parsed = await host.Client.PostAsJsonAsync("/api/assistant/parse", new
        {
            text = "اعمل فاتورة لنصرة، 3 كراتين فوايجر 4 لتر",
            locale = "ar"
        });
        var draft = await parsed.Content.ReadFromJsonAsync<AssistantDraftResponse>(Json);
        Assert.Equal("pending", draft!.Status);
        Assert.True(draft.CanApprove);
        var approved = await host.Client.PostAsync($"/api/assistant/drafts/{draft.DraftId}/approve", null);
        var result = await approved.Content.ReadFromJsonAsync<AssistantApproveResponse>(Json);
        Assert.Equal("approved", result!.Status);
        var invoice = await host.Client.GetFromJsonAsync<InvoiceResponse>($"/api/sales/invoices/{result.CreatedEntityId}", Json);
        Assert.Equal("draft", invoice!.Status);
    }

    private static async Task<Seed> SeedAsync(TestHost host)
    {
        await host.Client.PostAsJsonAsync("/api/auth/setup", TestHost.SampleSetup());
        await host.Client.PostAsync("/api/auth/logout", null);
        await host.Client.PostAsJsonAsync("/api/auth/login", new { userName = "father", password = "father1" });
        var customer = await host.Client.PostAsJsonAsync("/api/customers", NewCustomer("نصرة الشافعي"));
        var customerId = (await customer.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;
        var product = await host.Client.PostAsJsonAsync("/api/products", new
        {
            name = "فوايجر برونز",
            brand = "Voyager",
            category = "Oil",
            specification = "20W-50",
            isActive = true,
            imageRelativePath = (string?)null,
            variants = new[]
            {
                new
                {
                    packagingType = "كرتونة",
                    packagingSize = "4 لتر",
                    sku = "VB-4",
                    barcode = (string?)null,
                    minStock = 2m,
                    standardWholesalePrice = 5000m,
                    isActive = true
                }
            }
        });
        var productBody = await product.Content.ReadFromJsonAsync<ProductResponse>(Json);
        var warehouse = await host.Client.PostAsJsonAsync("/api/warehouses", new { name = "المخزن", isActive = true });
        var warehouseId = (await warehouse.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;
        var method = await host.Client.PostAsJsonAsync("/api/payment-methods", new { name = "نقدي", isActive = true });
        var methodId = (await method.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;
        await host.Client.PutAsJsonAsync("/api/settings/invoice-series", new { prefix = "PT", padding = 4 });
        var adjust = await host.Client.PostAsJsonAsync("/api/inventory/adjustments", new
        {
            warehouseId,
            direction = "in",
            reason = "رصيد اختبار",
            notes = (string?)null,
            occurredAt = "2026-08-01",
            lines = new[] { new { variantId = productBody!.Variants[0].Id, quantity = 50m } }
        });
        Assert.Equal(HttpStatusCode.Created, adjust.StatusCode);
        return new Seed(customerId, productBody.Id, productBody.Variants[0].Id, warehouseId, methodId);
    }

    private static async Task<InvoiceResponse> CreatePostedInvoiceAsync(TestHost host, Seed api, decimal qty, decimal price, string date = "2026-08-17")
    {
        var created = await host.Client.PostAsJsonAsync("/api/sales/invoices", new
        {
            customerId = api.CustomerId,
            warehouseId = api.WarehouseId,
            invoiceDate = date,
            dueDate = (string?)null,
            notes = (string?)null,
            discountAmount = 0m,
            manualTotal = (decimal?)null,
            manualTotalReason = (string?)null,
            lines = new[] { new { variantId = api.VariantId, quantity = qty, unitPrice = price, overrideReason = (string?)null } }
        });
        created.EnsureSuccessStatusCode();
        var invoice = (await created.Content.ReadFromJsonAsync<InvoiceResponse>(Json))!;
        var posted = await host.Client.PostAsJsonAsync($"/api/sales/invoices/{invoice.Id}/post", new { });
        posted.EnsureSuccessStatusCode();
        return (await posted.Content.ReadFromJsonAsync<InvoiceResponse>(Json))!;
    }

    private static object NewCustomer(string name) => new
    {
        name,
        customerTypeId = (Guid?)null,
        contactPerson = (string?)null,
        phone = (string?)null,
        whatsApp = (string?)null,
        address = (string?)null
    };

    private sealed record Seed(Guid CustomerId, Guid ProductId, Guid VariantId, Guid WarehouseId, Guid MethodId);
    private sealed record IdResponse(Guid Id);
    private sealed record CustomerListItem(Guid Id, string Name, bool IsActive);
    private sealed record CustomerResponse(Guid Id, string Name, bool IsActive);
    private sealed record ProductListItem(Guid Id, string Name, bool IsActive);
    private sealed record ProductResponse(Guid Id, VariantResponse[] Variants);
    private sealed record VariantResponse(Guid Id);
    private sealed record VariantPick(Guid Id);
    private sealed record PricingRow(Guid VariantId, decimal? BasePrice, int CustomerPriceCount);
    private sealed record PriceHistory(Guid CustomerId, decimal? NewUnitPrice);
    private sealed record InvoiceLineResponse(decimal Quantity, decimal? UnitPrice);
    private sealed record InvoiceResponse(
        Guid Id,
        string Status,
        decimal LinesSubtotal,
        decimal DiscountAmount,
        bool HasManualTotal,
        decimal GoodsTotal,
        DateTime CreatedAt,
        string? CreatedByName,
        InvoiceLineResponse[] Lines);
    private sealed record StatementLine(decimal Debit, decimal Credit, decimal RunningBalance);
    private sealed record StatementResponse(decimal OpeningBalance, decimal TotalDebits, decimal TotalCredits, decimal ClosingBalance, StatementLine[] Lines);
    private sealed record PaymentResponse(Guid Id, Guid CustomerId, decimal Amount);
    private sealed record ReportResponse(string Title, string[][] Rows);
    private sealed record AssistantAmbiguity(string Field, string Message);
    private sealed record AssistantDraftResponse(Guid DraftId, string IntentType, string Status, string SummaryAr, bool CanApprove, AssistantAmbiguity[] Ambiguities);
    private sealed record AssistantApproveResponse(string Status, Guid? CreatedEntityId);
}
