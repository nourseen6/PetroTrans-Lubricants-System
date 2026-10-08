using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetroTrans.Application.Operations;
using PetroTrans.Domain.Identity;
using PetroTrans.Infrastructure.Persistence;
using Xunit;

namespace PetroTrans.Api.Tests;

public class CoreWorkflowTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Scenario1_CreditSale_DraftPostPartialThenPaid_DoesNotChangeStockOnPayment()
    {
        await using var host = await TestHost.StartAsync();
        var api = await WorkflowApi.ReadyAsync(host);

        var draft = await api.CreateInvoiceAsync(api.CustomerId, api.WarehouseId, api.VariantId, 2, 10m);
        Assert.Equal("draft", draft.Status);
        Assert.Null(draft.Number);
        Assert.Equal(10m, await api.OnHandAsync());
        Assert.Equal(0m, await api.OutstandingAsync());

        var posted = await api.PostInvoiceAsync(draft.Id);
        Assert.Equal(HttpStatusCode.OK, posted.Status);
        var invoice = posted.Invoice!;
        Assert.Equal("posted", invoice.Status);
        Assert.Equal("unpaid", invoice.PaymentStatus);
        Assert.Equal(0m, invoice.PaidTotal);
        Assert.Equal(20m, invoice.RemainingTotal);
        Assert.False(string.IsNullOrWhiteSpace(invoice.Number));
        Assert.DoesNotContain("INV-0001", invoice.Number, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(8m, await api.OnHandAsync());
        Assert.Equal(20m, await api.OutstandingAsync());
        Assert.Empty((await api.TreasuryAsync()).Entries);

        var partial = await api.PayAsync(invoice.Id, 8m);
        Assert.Equal(HttpStatusCode.Created, partial.StatusCode);
        invoice = (await api.GetInvoiceAsync(invoice.Id))!;
        Assert.Equal("partial", invoice.PaymentStatus);
        Assert.Equal(8m, invoice.PaidTotal);
        Assert.Equal(12m, invoice.RemainingTotal);
        Assert.Equal(8m, await api.OnHandAsync());
        Assert.Equal(12m, await api.OutstandingAsync());
        Assert.Equal(8m, (await api.TreasuryAsync()).Balance);

        var finalPay = await api.PayAsync(invoice.Id, 12m);
        Assert.Equal(HttpStatusCode.Created, finalPay.StatusCode);
        invoice = (await api.GetInvoiceAsync(invoice.Id))!;
        Assert.Equal("paid", invoice.PaymentStatus);
        Assert.Equal(20m, invoice.PaidTotal);
        Assert.Equal(0m, invoice.RemainingTotal);
        Assert.Equal(8m, await api.OnHandAsync());
        Assert.Equal(0m, await api.OutstandingAsync());
        Assert.Equal(20m, (await api.TreasuryAsync()).Balance);

        using var scope = host.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Contains(await db.AuditLogs.Select(x => x.Action).ToListAsync(), x => x == "sales.post");
        Assert.Contains(await db.AuditLogs.Select(x => x.Action).ToListAsync(), x => x == "payments.create");
    }

    [Fact]
    public async Task EditDraftThenPost_RewritesLinesAndDecreasesStock()
    {
        await using var host = await TestHost.StartAsync();
        var api = await WorkflowApi.ReadyAsync(host);
        var draft = await api.CreateInvoiceAsync(api.CustomerId, api.WarehouseId, api.VariantId, 1, 10m);
        Assert.Equal("draft", draft.Status);
        Assert.Equal(10m, await api.OnHandAsync());

        var saved = await api.Client.PutAsJsonAsync($"/api/sales/invoices/{draft.Id}", new
        {
            customerId = api.CustomerId,
            warehouseId = api.WarehouseId,
            invoiceDate = "2026-08-17",
            dueDate = (string?)null,
            notes = (string?)null,
            discountAmount = 0m,
            manualTotal = (decimal?)null,
            manualTotalReason = (string?)null,
            lines = new[] { new { variantId = api.VariantId, quantity = 2m, unitPrice = 10m, overrideReason = (string?)null } }
        });
        Assert.True(saved.IsSuccessStatusCode, await ReadErrorAsync(saved));

        var posted = await api.PostInvoiceAsync(draft.Id);
        Assert.Equal(HttpStatusCode.OK, posted.Status);
        Assert.Equal("posted", posted.Invoice!.Status);
        Assert.Equal(20m, posted.Invoice.GoodsTotal);
        Assert.Equal(8m, await api.OnHandAsync());
        Assert.Equal(20m, await api.OutstandingAsync());
        Assert.Empty((await api.TreasuryAsync()).Entries);
    }

    [Fact]
    public async Task LinkingExistingTreasuryToPayment_DoesNotCreateSecondCashIn()
    {
        await using var host = await TestHost.StartAsync();
        var api = await WorkflowApi.ReadyAsync(host);
        var posted = (await api.PostInvoiceAsync((await api.CreateInvoiceAsync(api.CustomerId, api.WarehouseId, api.VariantId, 1, 10m)).Id)).Invoice!;
        Assert.Equal(10m, await api.OutstandingAsync());
        Assert.Empty((await api.TreasuryAsync()).Entries);

        await api.LoginAsync("father", "father1");
        var cash = await api.Client.PostAsJsonAsync("/api/treasury", new
        {
            occurredOn = "2026-08-17",
            direction = "in",
            category = "sales",
            description = "تحصيل دفتر",
            amount = 10m,
            notes = (string?)null
        });
        Assert.Equal(HttpStatusCode.Created, cash.StatusCode);
        var entry = await cash.Content.ReadFromJsonAsync<TreasuryEntryResponse>(Json);
        Assert.Equal(10m, (await api.TreasuryAsync()).Balance);

        var allocated = await api.Client.PostAsJsonAsync("/api/payments", new
        {
            customerId = api.CustomerId,
            paymentMethodId = api.MethodId,
            amount = 10m,
            paidOn = "2026-08-17",
            reference = (string?)null,
            notes = "ربط خزينة قائمة",
            existingTreasuryEntryId = entry!.Id
        });
        Assert.Equal(HttpStatusCode.Created, allocated.StatusCode);
        Assert.Equal(0m, await api.OutstandingAsync());
        var book = await api.TreasuryAsync();
        Assert.Equal(10m, book.TotalIn);
        Assert.Equal(10m, book.Balance);
        Assert.Single(book.Entries);

        var payment = await allocated.Content.ReadFromJsonAsync<IdResponse>(Json);
        var resized = await api.Client.PutAsJsonAsync($"/api/payments/{payment!.Id}", new
        {
            customerId = api.CustomerId,
            paymentMethodId = api.MethodId,
            amount = 8m,
            paidOn = "2026-08-17",
            reference = (string?)null,
            notes = "تعديل مبلغ مربوط"
        });
        Assert.Equal(HttpStatusCode.OK, resized.StatusCode);
        var afterEdit = await api.TreasuryAsync();
        Assert.Single(afterEdit.Entries);
        Assert.Equal(8m, afterEdit.Balance);
        Assert.Equal(8m, afterEdit.Entries[0].Amount);

        var voided = await api.Client.PostAsJsonAsync($"/api/payments/{payment.Id}/void", new { });
        Assert.Equal(HttpStatusCode.OK, voided.StatusCode);
        Assert.Equal(10m, await api.OutstandingAsync());
        Assert.Equal(8m, (await api.TreasuryAsync()).Balance);
        Assert.Single((await api.TreasuryAsync()).Entries);
    }

    [Fact]
    public async Task OwnerCanUnpostPostedInvoice_ToCorrectIt_RestoringStockAndBalance()
    {
        await using var host = await TestHost.StartAsync();
        var api = await WorkflowApi.ReadyAsync(host);
        var posted = (await api.PostInvoiceAsync((await api.CreateInvoiceAsync(api.CustomerId, api.WarehouseId, api.VariantId, 2, 10m)).Id)).Invoice!;
        Assert.Equal(8m, await api.OnHandAsync());
        Assert.Equal(20m, await api.OutstandingAsync());
        Assert.False(string.IsNullOrWhiteSpace(posted.Number));

        var denied = await api.Client.PostAsJsonAsync($"/api/sales/invoices/{posted.Id}/unpost", new { });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        await api.LoginAsync("father", "father1");
        var pay = await api.PayAsync(posted.Id, 8m);
        Assert.Equal(HttpStatusCode.Created, pay.StatusCode);
        Assert.Equal(12m, await api.OutstandingAsync());
        Assert.Equal(8m, (await api.TreasuryAsync()).Balance);

        var unposted = await api.Client.PostAsJsonAsync($"/api/sales/invoices/{posted.Id}/unpost", new { });
        Assert.Equal(HttpStatusCode.OK, unposted.StatusCode);
        var draft = await unposted.Content.ReadFromJsonAsync<InvoiceResponse>(Json);
        Assert.Equal("draft", draft!.Status);
        Assert.Equal(posted.Number, draft.Number);
        Assert.Equal(10m, await api.OnHandAsync());
        Assert.Equal(0m, await api.OutstandingAsync());
        Assert.Empty((await api.TreasuryAsync()).Entries);

        var reposted = await api.PostInvoiceAsync(posted.Id);
        Assert.Equal(HttpStatusCode.OK, reposted.Status);
        Assert.Equal(posted.Number, reposted.Invoice!.Number);
        Assert.Equal(8m, await api.OnHandAsync());
        Assert.Equal(20m, await api.OutstandingAsync());
    }

    [Fact]
    public async Task OwnerCanVoidPayment_RestoringInvoiceRemaining()
    {
        await using var host = await TestHost.StartAsync();
        var api = await WorkflowApi.ReadyAsync(host);
        var posted = (await api.PostInvoiceAsync((await api.CreateInvoiceAsync(api.CustomerId, api.WarehouseId, api.VariantId, 1, 10m)).Id)).Invoice!;
        var pay = await api.PayAsync(posted.Id, 4m);
        Assert.Equal(HttpStatusCode.Created, pay.StatusCode);
        var payment = await pay.Content.ReadFromJsonAsync<IdResponse>(Json);
        Assert.Equal(6m, (await api.GetInvoiceAsync(posted.Id))!.RemainingTotal);
        Assert.Equal(4m, (await api.TreasuryAsync()).Balance);

        var denied = await api.Client.PostAsJsonAsync($"/api/payments/{payment!.Id}/void", new { });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        await api.LoginAsync("father", "father1");
        var voided = await api.Client.PostAsJsonAsync($"/api/payments/{payment.Id}/void", new { });
        Assert.Equal(HttpStatusCode.OK, voided.StatusCode);
        Assert.Equal(10m, (await api.GetInvoiceAsync(posted.Id))!.RemainingTotal);
        Assert.Equal("unpaid", (await api.GetInvoiceAsync(posted.Id))!.PaymentStatus);
        Assert.Equal(10m, await api.OutstandingAsync());
        Assert.Equal(9m, await api.OnHandAsync());
        Assert.Empty((await api.TreasuryAsync()).Entries);
    }

    [Fact]
    public async Task SalesInvoice_DeletePostedUnpaid_ReversesStockAndBalance()
    {
        await using var host = await TestHost.StartAsync();
        var api = await WorkflowApi.ReadyAsync(host);
        var posted = (await api.PostInvoiceAsync((await api.CreateInvoiceAsync(api.CustomerId, api.WarehouseId, api.VariantId, 2, 10m)).Id)).Invoice!;
        Assert.Equal(8m, await api.OnHandAsync());
        Assert.Equal(20m, await api.OutstandingAsync());

        var denied = await api.Client.DeleteAsync($"/api/sales/invoices/{posted.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        await api.LoginAsync("father", "father1");
        var deleted = await api.Client.DeleteAsync($"/api/sales/invoices/{posted.Id}");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Null(await api.GetInvoiceAsync(posted.Id));
        Assert.Equal(10m, await api.OnHandAsync());
        Assert.Equal(0m, await api.OutstandingAsync());
    }

    [Fact]
    public async Task Payment_FifoAcrossTwoInvoices_AndReallocateWithoutDuplicateTreasury()
    {
        await using var host = await TestHost.StartAsync();
        var api = await WorkflowApi.ReadyAsync(host);
        await api.LoginAsync("father", "father1");
        var first = (await api.PostInvoiceAsync((await api.CreateInvoiceAsync(api.CustomerId, api.WarehouseId, api.VariantId, 1, 10000m, "2026-08-01")).Id)).Invoice!;
        var second = (await api.PostInvoiceAsync((await api.CreateInvoiceAsync(api.CustomerId, api.WarehouseId, api.VariantId, 1, 15000m, "2026-08-02")).Id)).Invoice!;
        Assert.Equal(25000m, await api.OutstandingAsync());

        var full = await api.Client.PostAsJsonAsync("/api/payments", new
        {
            customerId = api.CustomerId,
            paymentMethodId = api.MethodId,
            amount = 25000m,
            paidOn = "2026-08-17",
            reference = (string?)null,
            notes = (string?)null
        });
        Assert.Equal(HttpStatusCode.Created, full.StatusCode);
        Assert.Equal(0m, await api.OutstandingAsync());
        Assert.Equal("paid", (await api.GetInvoiceAsync(first.Id))!.PaymentStatus);
        Assert.Equal("paid", (await api.GetInvoiceAsync(second.Id))!.PaymentStatus);
        Assert.Equal(0m, (await api.GetInvoiceAsync(second.Id))!.RemainingTotal);
        Assert.Single((await api.TreasuryAsync()).Entries);
        Assert.Equal(25000m, (await api.TreasuryAsync()).Balance);

        await api.LoginAsync("father", "father1");
        var payment = await full.Content.ReadFromJsonAsync<IdResponse>(Json);
        var voided = await api.Client.PostAsJsonAsync($"/api/payments/{payment!.Id}/void", new { });
        Assert.Equal(HttpStatusCode.OK, voided.StatusCode);

        var partial = await api.Client.PostAsJsonAsync("/api/payments", new
        {
            customerId = api.CustomerId,
            paymentMethodId = api.MethodId,
            amount = 17000m,
            paidOn = "2026-08-17",
            reference = (string?)null,
            notes = (string?)null
        });
        Assert.Equal(HttpStatusCode.Created, partial.StatusCode);
        var partialPay = await partial.Content.ReadFromJsonAsync<IdResponse>(Json);
        Assert.Equal("paid", (await api.GetInvoiceAsync(first.Id))!.PaymentStatus);
        Assert.Equal(0m, (await api.GetInvoiceAsync(first.Id))!.RemainingTotal);
        Assert.Equal("partial", (await api.GetInvoiceAsync(second.Id))!.PaymentStatus);
        Assert.Equal(8000m, (await api.GetInvoiceAsync(second.Id))!.RemainingTotal);
        Assert.Equal(8000m, await api.OutstandingAsync());
        Assert.Single((await api.TreasuryAsync()).Entries);

        var moved = await api.Client.PutAsJsonAsync($"/api/payments/{partialPay!.Id}", new
        {
            customerId = api.CustomerId,
            paymentMethodId = api.MethodId,
            amount = 17000m,
            paidOn = "2026-08-17",
            reference = (string?)null,
            notes = (string?)null,
            allocations = new[]
            {
                new { invoiceId = second.Id, amount = 15000m },
                new { invoiceId = first.Id, amount = 2000m }
            }
        });
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal("partial", (await api.GetInvoiceAsync(first.Id))!.PaymentStatus);
        Assert.Equal(8000m, (await api.GetInvoiceAsync(first.Id))!.RemainingTotal);
        Assert.Equal("paid", (await api.GetInvoiceAsync(second.Id))!.PaymentStatus);
        Assert.Equal(8000m, await api.OutstandingAsync());
        Assert.Single((await api.TreasuryAsync()).Entries);
        Assert.Equal(17000m, (await api.TreasuryAsync()).Balance);

        var dropped = await api.Client.PutAsJsonAsync($"/api/payments/{partialPay.Id}", new
        {
            customerId = api.CustomerId,
            paymentMethodId = api.MethodId,
            amount = 17000m,
            paidOn = "2026-08-17",
            reference = (string?)null,
            notes = (string?)null,
            allocations = new[]
            {
                new { invoiceId = second.Id, amount = 15000m }
            }
        });
        Assert.Equal(HttpStatusCode.OK, dropped.StatusCode);
        Assert.Equal("unpaid", (await api.GetInvoiceAsync(first.Id))!.PaymentStatus);
        Assert.Equal(10000m, (await api.GetInvoiceAsync(first.Id))!.RemainingTotal);
        Assert.Equal("paid", (await api.GetInvoiceAsync(second.Id))!.PaymentStatus);
        Assert.Equal(8000m, await api.OutstandingAsync());
        Assert.Single((await api.TreasuryAsync()).Entries);
    }

    [Fact]
    public async Task PurchaseInvoice_DeletePosted_ReversesPayableAndStock()
    {
        await using var host = await TestHost.StartAsync();
        var api = await WorkflowApi.ReadyAsync(host, seedStock: false);
        var supplier = await api.CreateSupplierAsync();
        var billId = await api.SaveBillAsync(supplier, 7, 3m);
        Assert.Equal(HttpStatusCode.OK, (await api.Client.PostAsJsonAsync($"/api/purchase-invoices/{billId}/post", new { })).StatusCode);
        Assert.Equal(7m, await api.OnHandAsync());
        Assert.Equal(21m, await api.SupplierOutstandingAsync(supplier));

        var deleted = await api.Client.DeleteAsync($"/api/purchase-invoices/{billId}");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(0m, await api.OnHandAsync());
        Assert.Equal(0m, await api.SupplierOutstandingAsync(supplier));
    }

    [Fact]
    public async Task DraftDoesNotChangeStock_FailedPostRollsBack_InsufficientStockRejected()
    {
        await using var host = await TestHost.StartAsync();
        var api = await WorkflowApi.ReadyAsync(host);

        var draft = await api.CreateInvoiceAsync(api.CustomerId, api.WarehouseId, api.VariantId, 2, 10m);
        Assert.Equal(10m, await api.OnHandAsync());
        Assert.Equal(0m, await api.OutstandingAsync());

        var noSeriesHost = await TestHost.StartAsync();
        await using (noSeriesHost)
        {
            var bare = await WorkflowApi.ReadyAsync(noSeriesHost, configureSeries: false);
            var unpaidDraft = await bare.CreateInvoiceAsync(bare.CustomerId, bare.WarehouseId, bare.VariantId, 1, 10m);
            var blocked = await bare.Client.PostAsJsonAsync($"/api/sales/invoices/{unpaidDraft.Id}/post", new { });
            Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
            Assert.Equal("أضف ترقيم الفواتير من الإعدادات", await ReadErrorAsync(blocked));
            var stillDraft = await bare.GetInvoiceAsync(unpaidDraft.Id);
            Assert.Equal("draft", stillDraft!.Status);
            Assert.Null(stillDraft.Number);
            Assert.Equal(10m, await bare.OnHandAsync());
            Assert.Equal(0m, await bare.OutstandingAsync());
        }

        var oversell = await api.CreateInvoiceAsync(api.CustomerId, api.WarehouseId, api.VariantId, 99, 10m);
        var rejected = await api.Client.PostAsJsonAsync($"/api/sales/invoices/{oversell.Id}/post", new { });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Contains("الكمية المتاحة غير كافية", await ReadErrorAsync(rejected));
        Assert.Equal("draft", (await api.GetInvoiceAsync(oversell.Id))!.Status);
        Assert.Equal(10m, await api.OnHandAsync());
        Assert.Equal(0m, await api.OutstandingAsync());
    }

    [Fact]
    public async Task Scenario2_PurchaseInvoiceIncreasesStockThenSale()
    {
        await using var host = await TestHost.StartAsync();
        var api = await WorkflowApi.ReadyAsync(host, seedStock: false);
        Assert.Equal(0m, await api.OnHandAsync());

        var supplier = await api.CreateSupplierAsync();
        var bill = await api.SaveBillAsync(supplier, 7, 3m);
        var postedBill = await api.Client.PostAsJsonAsync($"/api/purchase-invoices/{bill}/post", new { });
        Assert.Equal(HttpStatusCode.OK, postedBill.StatusCode);
        Assert.Equal(7m, await api.OnHandAsync());
        Assert.Equal(21m, await api.SupplierOutstandingAsync(supplier));

        var sale = await api.CreateInvoiceAsync(api.CustomerId, api.WarehouseId, api.VariantId, 2, 10m);
        var postedSale = await api.PostInvoiceAsync(sale.Id);
        Assert.Equal(HttpStatusCode.OK, postedSale.Status);
        Assert.Equal(5m, await api.OnHandAsync());

        using var scope = host.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Contains(await db.AuditLogs.Select(x => x.Action).ToListAsync(), x => x == "purchasing.post");
        Assert.Equal(1, await db.InventoryMovements.CountAsync(x => x.SourceDocumentType == "purchase_invoice"));
    }

    [Fact]
    public async Task PostedPurchaseInvoice_VerifiedHeader_UpdatesTotalWithoutChangingLinesOrStock()
    {
        await using var host = await TestHost.StartAsync();
        var api = await WorkflowApi.ReadyAsync(host, seedStock: false);
        var supplier = await api.CreateSupplierAsync();
        var billId = await api.SaveBillAsync(supplier, 7, 3m);
        Assert.Equal(HttpStatusCode.OK, (await api.Client.PostAsJsonAsync($"/api/purchase-invoices/{billId}/post", new { })).StatusCode);
        Assert.Equal(7m, await api.OnHandAsync());
        Assert.Equal(21m, await api.SupplierOutstandingAsync(supplier));

        using var scope = host.App.Services.CreateScope();
        var purchasing = scope.ServiceProvider.GetRequiredService<IPurchasingService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var actor = await db.Users.Select(x => x.Id).FirstAsync();
        var before = await purchasing.GetBillAsync(billId);
        Assert.NotNull(before);
        Assert.Equal(21m, before.GoodsTotal);
        Assert.Equal(7m, before.Lines[0].Quantity);
        Assert.Equal(3m, before.Lines[0].UnitPrice);

        var updated = await purchasing.SetPostedVerifiedHeaderAsync(billId, "808364", 808364m, actor);
        Assert.True(updated.Succeeded, updated.Error);
        Assert.Equal("808364", updated.Value!.Number);
        Assert.Equal(808364m, updated.Value.GoodsTotal);
        Assert.Equal(before.Lines[0].VariantId, updated.Value.Lines[0].VariantId);
        Assert.Equal(7m, updated.Value.Lines[0].Quantity);
        Assert.Equal(3m, updated.Value.Lines[0].UnitPrice);
        Assert.Equal(21m, updated.Value.Lines[0].LineTotal);
        Assert.Equal(7m, await api.OnHandAsync());
        Assert.Equal(808364m, await api.SupplierOutstandingAsync(supplier));

        var again = await purchasing.SetPostedVerifiedHeaderAsync(billId, "808364", 808364m, actor);
        Assert.True(again.Succeeded, again.Error);
        Assert.Equal(1, await db.PurchaseInvoices.CountAsync());
        Assert.Equal(1, await db.PurchaseInvoiceLines.CountAsync());
        Assert.Contains(await db.AuditLogs.Select(x => x.Action).ToListAsync(), x => x == "purchasing.verified_header");
    }

    [Fact]
    public async Task SupplierPayment_ReducesPayable_DoesNotChangeStock_AndOverpayIsRejected()
    {
        await using var host = await TestHost.StartAsync();
        var api = await WorkflowApi.ReadyAsync(host, seedStock: false);

        var supplier = await api.CreateSupplierAsync();
        var bill = await api.SaveBillAsync(supplier, 7, 3m);
        Assert.Equal(HttpStatusCode.OK, (await api.Client.PostAsJsonAsync($"/api/purchase-invoices/{bill}/post", new { })).StatusCode);
        Assert.Equal(7m, await api.OnHandAsync());
        Assert.Equal(21m, await api.SupplierOutstandingAsync(supplier));

        var over = await api.PaySupplierAsync(supplier, 50m, "2026-08-17");
        Assert.Equal(HttpStatusCode.BadRequest, over.StatusCode);
        Assert.Equal("المبلغ لا يمكن أن يتجاوز المستحق للمورد.", await ReadErrorAsync(over));
        Assert.Equal(21m, await api.SupplierOutstandingAsync(supplier));
        Assert.Equal(7m, await api.OnHandAsync());

        var paid = await api.PaySupplierAsync(supplier, 8m, "2026-08-15");
        Assert.Equal(HttpStatusCode.Created, paid.StatusCode);
        Assert.Equal(13m, await api.SupplierOutstandingAsync(supplier));
        Assert.Equal(7m, await api.OnHandAsync());
        Assert.Equal(8m, (await api.TreasuryAsync()).TotalOut);
        Assert.Equal(-8m, (await api.TreasuryAsync()).Balance);

        var payment = await paid.Content.ReadFromJsonAsync<IdResponse>(Json);
        var deleted = await api.Client.DeleteAsync($"/api/supplier-payments/{payment!.Id}");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(21m, await api.SupplierOutstandingAsync(supplier));
        Assert.Equal(7m, await api.OnHandAsync());
        Assert.Equal(0m, (await api.TreasuryAsync()).TotalOut);
        Assert.Equal(0m, (await api.TreasuryAsync()).Balance);

        using var scope = host.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await db.SupplierPayments.CountAsync());
        Assert.Equal(0, await db.Payments.CountAsync());
        Assert.Contains(await db.AuditLogs.Select(x => x.Action).ToListAsync(), x => x == "payments.supplier");
        Assert.Contains(await db.AuditLogs.Select(x => x.Action).ToListAsync(), x => x == "payments.supplier_void");
    }

    [Fact]
    public async Task SupplierStatement_ShowsBillsPaymentsAndOutstanding()
    {
        await using var host = await TestHost.StartAsync();
        var api = await WorkflowApi.ReadyAsync(host, seedStock: false);

        var supplier = await api.CreateSupplierAsync();
        var bill = await api.SaveBillAsync(supplier, 7, 3m);
        Assert.Equal(HttpStatusCode.OK, (await api.Client.PostAsJsonAsync($"/api/purchase-invoices/{bill}/post", new { })).StatusCode);
        var paid = await api.PaySupplierAsync(supplier, 8m, "2026-08-15");
        Assert.Equal(HttpStatusCode.Created, paid.StatusCode);

        var statement = await api.Client.GetFromJsonAsync<SupplierStatementResponse>($"/api/suppliers/{supplier}/statement", Json);
        Assert.NotNull(statement);
        Assert.Equal(0m, statement.OpeningBalance);
        Assert.Equal(21m, statement.TotalDebits);
        Assert.Equal(8m, statement.TotalCredits);
        Assert.Equal(13m, statement.ClosingBalance);
        Assert.Equal(13m, statement.Outstanding);
        Assert.Equal(2, statement.Lines.Length);
        Assert.Contains(statement.Lines, x => x.Description == "فاتورة مشتريات" && x.Debit == 21m);
        Assert.Contains(statement.Lines, x => x.Description == "دفعة للمورد" && x.Credit == 8m);
        Assert.Equal(13m, statement.Lines[^1].RunningBalance);

        var missing = await api.Client.GetAsync($"/api/suppliers/{Guid.NewGuid()}/statement");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Scenario3_ReturnRestoresStock_AndCreditsOriginalInvoicePrice()
    {
        await using var host = await TestHost.StartAsync();
        var api = await WorkflowApi.ReadyAsync(host);
        var sale = await api.CreateInvoiceAsync(api.CustomerId, api.WarehouseId, api.VariantId, 2, 10m);
        var posted = (await api.PostInvoiceAsync(sale.Id)).Invoice!;
        Assert.Equal(8m, await api.OnHandAsync());
        Assert.Equal(20m, await api.OutstandingAsync());

        var created = await api.Client.PostAsJsonAsync("/api/sales-returns", new
        {
            customerId = api.CustomerId,
            originalSalesInvoiceId = posted.Id,
            warehouseId = api.WarehouseId,
            returnDate = "2026-08-17",
            notes = (string?)null,
            lines = new[] { new { variantId = api.VariantId, quantity = 2m } }
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var ret = await created.Content.ReadFromJsonAsync<IdResponse>(Json);
        var postedReturn = await api.Client.PostAsJsonAsync($"/api/sales-returns/{ret!.Id}/post", new { });
        Assert.Equal(HttpStatusCode.OK, postedReturn.StatusCode);
        var body = await postedReturn.Content.ReadFromJsonAsync<ReturnResponse>(Json);
        Assert.Equal("posted", body!.Status);
        Assert.Equal("posted", body.BalancePostingStatus);
        Assert.Equal(10m, await api.OnHandAsync());
        Assert.Equal(0m, await api.OutstandingAsync());
        var invoice = (await api.GetInvoiceAsync(posted.Id))!;
        Assert.Equal(0m, invoice.RemainingTotal);
        Assert.Equal("paid", invoice.PaymentStatus);

        using var scope = host.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Contains(await db.PartyLedgerEntries.Select(x => x.EntryType).ToListAsync(), x => x == "sales_return");
        Assert.Contains(await db.AuditLogs.Select(x => x.Action).ToListAsync(), x => x == "returns.create");
    }

    [Fact]
    public async Task PartialReturn_RestoresPartOfStock_AndReducesRemaining()
    {
        await using var host = await TestHost.StartAsync();
        var api = await WorkflowApi.ReadyAsync(host);
        var sale = await api.CreateInvoiceAsync(api.CustomerId, api.WarehouseId, api.VariantId, 2, 10m);
        var posted = (await api.PostInvoiceAsync(sale.Id)).Invoice!;
        Assert.Equal(8m, await api.OnHandAsync());

        var created = await api.Client.PostAsJsonAsync("/api/sales-returns", new
        {
            customerId = api.CustomerId,
            originalSalesInvoiceId = posted.Id,
            warehouseId = api.WarehouseId,
            returnDate = "2026-08-17",
            notes = (string?)null,
            lines = new[] { new { variantId = api.VariantId, quantity = 1m } }
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var ret = await created.Content.ReadFromJsonAsync<IdResponse>(Json);
        var postedReturn = await api.Client.PostAsJsonAsync($"/api/sales-returns/{ret!.Id}/post", new { });
        Assert.Equal(HttpStatusCode.OK, postedReturn.StatusCode);
        Assert.Equal(9m, await api.OnHandAsync());
        Assert.Equal(10m, await api.OutstandingAsync());
        var invoice = (await api.GetInvoiceAsync(posted.Id))!;
        Assert.Equal(10m, invoice.RemainingTotal);
        Assert.Equal("partial", invoice.PaymentStatus);

        var tooMuch = await api.Client.PostAsJsonAsync("/api/sales-returns", new
        {
            customerId = api.CustomerId,
            originalSalesInvoiceId = posted.Id,
            warehouseId = api.WarehouseId,
            returnDate = "2026-08-17",
            notes = (string?)null,
            lines = new[] { new { variantId = api.VariantId, quantity = 2m } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
    }

    [Fact]
    public async Task Scenario4_AdjustmentRequiresReason_ChangesStock_AndIsAudited()
    {
        await using var host = await TestHost.StartAsync();
        var api = await WorkflowApi.ReadyAsync(host, seedStock: false);

        var missingReason = await api.Client.PostAsJsonAsync("/api/inventory/adjustments", new
        {
            warehouseId = api.WarehouseId,
            direction = "in",
            reason = "",
            notes = (string?)null,
            occurredAt = "2026-08-17",
            lines = new[] { new { variantId = api.VariantId, quantity = 4m } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);
        Assert.Equal(0m, await api.OnHandAsync());

        var added = await api.AdjustAsync(4m, "in", "جرد");
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        Assert.Equal(4m, await api.OnHandAsync());
        var removed = await api.AdjustAsync(1m, "out", "تلف");
        Assert.Equal(HttpStatusCode.Created, removed.StatusCode);
        Assert.Equal(3m, await api.OnHandAsync());

        using var scope = host.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await db.AuditLogs.CountAsync(x => x.Action == "inventory.adjust"));
    }

    [Fact]
    public async Task Adjustment_CanBeEditedAndDeleted_AndBlockedWhenStockWouldGoNegative()
    {
        await using var host = await TestHost.StartAsync();
        var api = await WorkflowApi.ReadyAsync(host, seedStock: false);

        var added = await api.AdjustAsync(4m, "in", "جرد");
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var created = await added.Content.ReadFromJsonAsync<IdResponse>(Json);
        Assert.NotNull(created);
        Assert.Equal(4m, await api.OnHandAsync());

        var updated = await api.Client.PutAsJsonAsync($"/api/inventory/adjustments/{created.Id}", new
        {
            warehouseId = api.WarehouseId,
            direction = "in",
            reason = "جرد معدل",
            notes = (string?)null,
            occurredAt = "2026-08-17",
            lines = new[] { new { variantId = api.VariantId, quantity = 7m } }
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(7m, await api.OnHandAsync());

        var listed = await api.Client.GetFromJsonAsync<AdjustmentListRow[]>("/api/inventory/adjustments", Json);
        Assert.NotNull(listed);
        Assert.Single(listed);
        Assert.Equal("جرد معدل", listed[0].Reason);
        Assert.Equal(7m, listed[0].Lines[0].Quantity);

        var removedQty = await api.AdjustAsync(3m, "out", "تلف");
        Assert.Equal(HttpStatusCode.Created, removedQty.StatusCode);
        Assert.Equal(4m, await api.OnHandAsync());

        var blocked = await api.Client.DeleteAsync($"/api/inventory/adjustments/{created.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Equal(4m, await api.OnHandAsync());

        var outRow = await removedQty.Content.ReadFromJsonAsync<IdResponse>(Json);
        Assert.NotNull(outRow);
        var deletedOut = await api.Client.DeleteAsync($"/api/inventory/adjustments/{outRow.Id}");
        Assert.Equal(HttpStatusCode.OK, deletedOut.StatusCode);
        Assert.Equal(7m, await api.OnHandAsync());

        var deletedIn = await api.Client.DeleteAsync($"/api/inventory/adjustments/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, deletedIn.StatusCode);
        Assert.Equal(0m, await api.OnHandAsync());
        Assert.Empty((await api.Client.GetFromJsonAsync<AdjustmentListRow[]>("/api/inventory/adjustments", Json))!);

        using var scope = host.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await db.InventoryAdjustments.CountAsync());
        Assert.Equal(0, await db.InventoryMovements.CountAsync());
        Assert.Contains(await db.AuditLogs.Select(x => x.Action).ToListAsync(), x => x == "inventory.adjust_update");
        Assert.Contains(await db.AuditLogs.Select(x => x.Action).ToListAsync(), x => x == "inventory.adjust_void");
    }

    [Fact]
    public async Task Scenario5_FatherCanOverridePrice_OperatorIsDenied()
    {
        await using var host = await TestHost.StartAsync();
        var api = await WorkflowApi.ReadyAsync(host);

        var denied = await api.Client.PostAsJsonAsync("/api/sales/invoices", api.InvoiceBody(15m));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal("غير مسموح بتغيير السعر.", await ReadErrorAsync(denied));

        await api.LoginAsync("father", "father1");
        var allowed = await api.Client.PostAsJsonAsync("/api/sales/invoices", api.InvoiceBody(15m, "سعر خاص"));
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);

        using var scope = host.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Contains(await db.AuditLogs.Where(x => x.Action == "pricing.override").Select(x => x.AfterJson).ToListAsync(), json => json is not null && json.Contains("15", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Scenario6_Security_UnauthenticatedAndUnauthorizedAreRejected()
    {
        await using var host = await TestHost.StartAsync();
        var unauthInvoice = await host.AnonymousClient.GetAsync("/api/sales/invoices");
        var unauthPay = await host.AnonymousClient.PostAsJsonAsync("/api/payments", new
        {
            invoiceId = Guid.NewGuid(),
            paymentMethodId = Guid.NewGuid(),
            amount = 1m,
            paidOn = "2026-08-17",
            reference = (string?)null,
            notes = (string?)null
        });
        Assert.Equal(HttpStatusCode.Unauthorized, unauthInvoice.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthPay.StatusCode);

        var api = await WorkflowApi.ReadyAsync(host);
        var me = await api.Client.GetFromJsonAsync<AuthUserResponse>("/api/auth/me", Json);
        Assert.DoesNotContain(PermissionCodes.RolesManage, me!.Permissions);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client.GetAsync("/api/secure/roles-manage")).StatusCode);

        await api.LoginAsync("father", "father1");
        var father = await api.Client.GetFromJsonAsync<AuthUserResponse>("/api/auth/me", Json);
        Assert.Contains(PermissionCodes.PricingOverride, father!.Permissions);
        Assert.DoesNotContain(PermissionCodes.RolesManage, father.Permissions);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client.GetAsync("/api/secure/roles-manage")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Payments_OverpayRejected_AndReportsUseRealData()
    {
        await using var host = await TestHost.StartAsync();
        var api = await WorkflowApi.ReadyAsync(host);
        var posted = (await api.PostInvoiceAsync((await api.CreateInvoiceAsync(api.CustomerId, api.WarehouseId, api.VariantId, 1, 10m)).Id)).Invoice!;

        var over = await api.PayAsync(posted.Id, 50m);
        Assert.Equal(HttpStatusCode.BadRequest, over.StatusCode);
        Assert.Equal("المبلغ لا يمكن أن يتجاوز المتبقي على الفاتورة.", await ReadErrorAsync(over));
        Assert.Equal("unpaid", (await api.GetInvoiceAsync(posted.Id))!.PaymentStatus);
        Assert.Equal(9m, await api.OnHandAsync());

        var sales = await api.Client.GetFromJsonAsync<ReportResponse>("/api/reports/sales", Json);
        Assert.NotEmpty(sales!.Rows);
        var unpaid = await api.Client.GetFromJsonAsync<ReportResponse>("/api/reports/unpaid", Json);
        Assert.NotEmpty(unpaid!.Rows);
        var statement = await api.Client.GetFromJsonAsync<StatementResponse>($"/api/customers/{api.CustomerId}/statement", Json);
        Assert.Equal(10m, statement!.Outstanding);
    }

    [Fact]
    public async Task BackupCopiesLiveDatabase_RestoreRequiresConfirmation_AndIsAudited()
    {
        await using var host = await TestHost.StartAsync();
        var api = await WorkflowApi.ReadyAsync(host);
        var backupPath = Path.Combine(Path.GetTempPath(), $"petrotrans-bak-{Guid.NewGuid():N}.db");
        try
        {
            var backup = await api.Client.PostAsJsonAsync("/api/backup", new { destinationPath = backupPath });
            Assert.Equal(HttpStatusCode.OK, backup.StatusCode);
            Assert.True(File.Exists(backupPath));
            Assert.True(File.Exists(host.DatabasePath));

            var extra = await api.CreateSupplierAsync("S-2");
            var denied = await api.Client.PostAsJsonAsync("/api/restore", new { sourcePath = backupPath, confirm = false });
            Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
            Assert.Equal("سيتم استبدال البيانات الحالية", await ReadErrorAsync(denied));
            Assert.NotNull(await api.Client.GetFromJsonAsync<IdResponse>($"/api/suppliers/{extra}", Json));

            var restored = await api.Client.PostAsJsonAsync("/api/restore", new { sourcePath = backupPath, confirm = true });
            Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
            var missing = await api.Client.GetAsync($"/api/suppliers/{extra}");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

            using var scope = host.App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Contains(await db.AuditLogs.Select(x => x.Action).ToListAsync(), x => x == "backup.create" || x == "backup.restore");
        }
        finally
        {
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(Json);
        return body?.Error ?? "";
    }

    private sealed class WorkflowApi
    {
        private WorkflowApi(TestHost host, Guid customerId, Guid variantId, Guid warehouseId, Guid methodId)
        {
            Host = host;
            CustomerId = customerId;
            VariantId = variantId;
            WarehouseId = warehouseId;
            MethodId = methodId;
        }

        public TestHost Host { get; }
        public HttpClient Client => Host.Client;
        public Guid CustomerId { get; }
        public Guid VariantId { get; }
        public Guid WarehouseId { get; }
        public Guid MethodId { get; }

        public static async Task<WorkflowApi> ReadyAsync(TestHost host, bool seedStock = true, bool configureSeries = true)
        {
            await host.Client.PostAsJsonAsync("/api/auth/setup", TestHost.SampleSetup());
            var customer = await host.Client.PostAsJsonAsync("/api/customers", new
            {
                name = "عميل تشغيلي",
                customerTypeId = (Guid?)null,
                contactPerson = "",
                phone = "",
                whatsApp = "",
                address = ""
            });
            var customerId = (await customer.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;
            var product = await host.Client.PostAsJsonAsync("/api/products", new
            {
                name = "صنف تشغيلي",
                brand = "",
                category = "",
                specification = "",
                isActive = true,
                imageRelativePath = (string?)null,
                variants = new[]
                {
                    new
                    {
                        packagingType = "كرتونة",
                        packagingSize = "3X4",
                        sku = (string?)null,
                        barcode = (string?)null,
                        minStock = (decimal?)null,
                        standardWholesalePrice = 10m,
                        isActive = true
                    }
                }
            });
            var variantId = (await product.Content.ReadFromJsonAsync<ProductResponse>(Json))!.Variants[0].Id;
            var warehouse = await host.Client.PostAsJsonAsync("/api/warehouses", new { name = "المخزن التشغيلي", isActive = true });
            var warehouseId = (await warehouse.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;
            var method = await host.Client.PostAsJsonAsync("/api/payment-methods", new { name = "تحويل", isActive = true });
            var methodId = (await method.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;
            if (configureSeries)
            {
                await host.Client.PutAsJsonAsync("/api/settings/invoice-series", new { prefix = "PT", padding = 4 });
            }

            var api = new WorkflowApi(host, customerId, variantId, warehouseId, methodId);
            if (seedStock)
            {
                var stocked = await api.AdjustAsync(10m, "in", "رصيد اختبار");
                Assert.Equal(HttpStatusCode.Created, stocked.StatusCode);
            }

            return api;
        }

        public object InvoiceBody(decimal? unitPrice, string? reason = null) => new
        {
            customerId = CustomerId,
            warehouseId = WarehouseId,
            invoiceDate = "2026-08-17",
            dueDate = (string?)null,
            notes = (string?)null,
            lines = new[]
            {
                new { variantId = VariantId, quantity = 1m, unitPrice, overrideReason = reason }
            }
        };

        public async Task<InvoiceResponse> CreateInvoiceAsync(Guid customerId, Guid warehouseId, Guid variantId, decimal qty, decimal? price, string invoiceDate = "2026-08-17")
        {
            var created = await Client.PostAsJsonAsync("/api/sales/invoices", new
            {
                customerId,
                warehouseId,
                invoiceDate,
                dueDate = (string?)null,
                notes = (string?)null,
                lines = new[] { new { variantId, quantity = qty, unitPrice = price, overrideReason = (string?)null } }
            });
            created.EnsureSuccessStatusCode();
            return (await created.Content.ReadFromJsonAsync<InvoiceResponse>(Json))!;
        }

        public async Task<(HttpStatusCode Status, InvoiceResponse? Invoice)> PostInvoiceAsync(Guid id)
        {
            var response = await Client.PostAsJsonAsync($"/api/sales/invoices/{id}/post", new { });
            var invoice = response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<InvoiceResponse>(Json) : null;
            return (response.StatusCode, invoice);
        }

        public async Task<InvoiceResponse?> GetInvoiceAsync(Guid id)
        {
            var response = await Client.GetAsync($"/api/sales/invoices/{id}");
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<InvoiceResponse>(Json);
        }

        public Task<HttpResponseMessage> PayAsync(Guid invoiceId, decimal amount)
            => Client.PostAsJsonAsync("/api/payments", new
            {
                invoiceId,
                paymentMethodId = MethodId,
                amount,
                paidOn = "2026-08-17",
                reference = (string?)null,
                notes = (string?)null
            });

        public Task<HttpResponseMessage> PaySupplierAsync(Guid supplierId, decimal amount, string paidOn)
            => Client.PostAsJsonAsync("/api/supplier-payments", new
            {
                supplierId,
                paymentMethodId = MethodId,
                amount,
                paidOn,
                reference = (string?)null,
                notes = (string?)null
            });

        public Task<HttpResponseMessage> AdjustAsync(decimal quantity, string direction, string reason)
            => Client.PostAsJsonAsync("/api/inventory/adjustments", new
            {
                warehouseId = WarehouseId,
                direction,
                reason,
                notes = (string?)null,
                occurredAt = "2026-08-17",
                lines = new[] { new { variantId = VariantId, quantity } }
            });

        public async Task<decimal> OnHandAsync()
        {
            var rows = await Client.GetFromJsonAsync<StockRow[]>($"/api/inventory/on-hand?warehouseId={WarehouseId}", Json);
            return rows?.FirstOrDefault(x => x.VariantId == VariantId)?.OnHand ?? 0m;
        }

        public async Task<decimal> OutstandingAsync()
        {
            var statement = await Client.GetFromJsonAsync<StatementResponse>($"/api/customers/{CustomerId}/statement", Json);
            return statement?.Outstanding ?? 0m;
        }

        public async Task<TreasuryBookResponse> TreasuryAsync()
            => (await Client.GetFromJsonAsync<TreasuryBookResponse>("/api/treasury", Json))!;

        public async Task<Guid> CreateSupplierAsync(string code = "S-1")
        {
            var created = await Client.PostAsJsonAsync("/api/suppliers", new
            {
                code,
                name = "مورد تشغيلي",
                phone = "",
                address = "",
                isActive = true
            });
            created.EnsureSuccessStatusCode();
            return (await created.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;
        }

        public async Task<Guid> SaveReceiptAsync(Guid supplierId, decimal qty)
        {
            var created = await Client.PostAsJsonAsync("/api/goods-receipts", new
            {
                supplierId,
                warehouseId = WarehouseId,
                documentDate = "2026-08-17",
                notes = (string?)null,
                lines = new[] { new { variantId = VariantId, quantity = qty } }
            });
            created.EnsureSuccessStatusCode();
            return (await created.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;
        }

        public async Task<Guid> SaveBillAsync(Guid supplierId, decimal qty, decimal price)
        {
            var created = await Client.PostAsJsonAsync("/api/purchase-invoices", new
            {
                supplierId,
                invoiceDate = "2026-08-17",
                notes = (string?)null,
                lines = new[] { new { variantId = VariantId, quantity = qty, unitPrice = price } }
            });
            created.EnsureSuccessStatusCode();
            return (await created.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;
        }

        public async Task<decimal> SupplierOutstandingAsync(Guid supplierId)
        {
            var supplier = await Client.GetFromJsonAsync<SupplierResponse>($"/api/suppliers/{supplierId}", Json);
            return supplier?.Outstanding ?? 0m;
        }

        public async Task LoginAsync(string userName, string password)
        {
            await Client.PostAsync("/api/auth/logout", null);
            var login = await Client.PostAsJsonAsync("/api/auth/login", new { userName, password });
            login.EnsureSuccessStatusCode();
        }
    }

    private sealed record IdResponse(Guid Id);
    private sealed record AdjustmentListRow(string Reason, AdjustmentListLine[] Lines);
    private sealed record AdjustmentListLine(decimal Quantity);
    private sealed record ProductResponse(VariantResponse[] Variants);
    private sealed record VariantResponse(Guid Id);
    private sealed record InvoiceResponse(
        Guid Id,
        string? Number,
        string Status,
        decimal GoodsTotal,
        decimal PaidTotal,
        decimal RemainingTotal,
        string PaymentStatus);
    private sealed record StockRow(Guid VariantId, decimal OnHand);
    private sealed record StatementResponse(decimal Outstanding);
    private sealed record SupplierStatementResponse(
        Guid SupplierId,
        decimal OpeningBalance,
        decimal TotalDebits,
        decimal TotalCredits,
        decimal ClosingBalance,
        decimal Outstanding,
        SupplierStatementLine[] Lines);
    private sealed record SupplierStatementLine(string Description, decimal Debit, decimal Credit, decimal RunningBalance);
    private sealed record SupplierResponse(decimal Outstanding);
    private sealed record ReturnResponse(string Status, string BalancePostingStatus);
    private sealed record ReportResponse(string Title, List<List<string?>> Rows);
    private sealed record AuthUserResponse(string[] Permissions);
    private sealed record ErrorResponse(string Error);
    private sealed record TreasuryBookResponse(decimal TotalIn, decimal TotalOut, decimal Balance, TreasuryRow[] Entries);
    private sealed record TreasuryRow(string Direction, decimal Amount, string? Category);
    private sealed record TreasuryEntryResponse(Guid Id, decimal Amount);
}
