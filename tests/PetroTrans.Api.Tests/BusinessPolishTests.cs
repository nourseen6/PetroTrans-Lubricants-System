using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PetroTrans.Api.Tests;

public class BusinessPolishTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task ManualTotalOverride_RequiresReason_AndDoesNotChangeLinePrices()
    {
        await using var host = await TestHost.StartAsync();
        await host.Client.PostAsJsonAsync("/api/auth/setup", TestHost.SampleSetup());
        await host.Client.PostAsync("/api/auth/logout", null);
        var fatherLogin = await host.Client.PostAsJsonAsync("/api/auth/login", new { userName = "father", password = "father1" });
        Assert.Equal(HttpStatusCode.OK, fatherLogin.StatusCode);

        var customer = await host.Client.PostAsJsonAsync("/api/customers", new
        {
            name = "نصره",
            customerTypeId = (Guid?)null,
            contactPerson = (string?)null,
            phone = (string?)null,
            whatsApp = (string?)null,
            address = (string?)null
        });
        var customerBody = await customer.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        var product = await host.Client.PostAsJsonAsync("/api/products", new
        {
            name = "فوايجر",
            brand = "Voyager",
            category = "Oil",
            specification = "5W-40",
            isActive = true,
            imageRelativePath = (string?)null,
            variants = new[]
            {
                new
                {
                    packagingType = "كرتونة",
                    packagingSize = "1 لتر",
                    sku = "V-1",
                    barcode = (string?)null,
                    minStock = (decimal?)null,
                    standardWholesalePrice = 5000m,
                    isActive = true
                }
            }
        });
        var productBody = await product.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);

        var denied = await host.Client.PostAsJsonAsync("/api/sales/invoices", new
        {
            customerId = customerBody!.Id,
            warehouseId = (Guid?)null,
            invoiceDate = "2026-08-20",
            dueDate = (string?)null,
            notes = (string?)null,
            discountAmount = 0m,
            manualTotal = 9500m,
            manualTotalReason = (string?)null,
            lines = new[]
            {
                new { variantId = productBody!.Variants[0].Id, quantity = 2m, unitPrice = (decimal?)5000m, overrideReason = (string?)null }
            }
        });
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);

        var created = await host.Client.PostAsJsonAsync("/api/sales/invoices", new
        {
            customerId = customerBody.Id,
            warehouseId = (Guid?)null,
            invoiceDate = "2026-08-20",
            dueDate = (string?)null,
            notes = (string?)null,
            discountAmount = 0m,
            manualTotal = 9500m,
            manualTotalReason = "سعر خاص للعميل",
            lines = new[]
            {
                new { variantId = productBody.Variants[0].Id, quantity = 2m, unitPrice = (decimal?)5000m, overrideReason = (string?)null }
            }
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var invoice = await created.Content.ReadFromJsonAsync<InvoiceResponse>(JsonOptions);
        Assert.True(invoice!.HasManualTotal);
        Assert.Equal(10000m, invoice.LinesSubtotal);
        Assert.Equal(9500m, invoice.GoodsTotal);
        Assert.Equal(5000m, invoice.Lines[0].UnitPrice);
    }

    [Fact]
    public async Task OnAccountPayment_UpdatesCustomerLedger_WithoutInvoice()
    {
        await using var host = await TestHost.StartAsync();
        await host.Client.PostAsJsonAsync("/api/auth/setup", TestHost.SampleSetup());
        var customer = await host.Client.PostAsJsonAsync("/api/customers", new
        {
            name = "عواد",
            customerTypeId = (Guid?)null,
            contactPerson = (string?)null,
            phone = (string?)null,
            whatsApp = (string?)null,
            address = (string?)null
        });
        var customerBody = await customer.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        var method = await host.Client.PostAsJsonAsync("/api/payment-methods", new { name = "نقدي", isActive = true });
        var methodBody = await method.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);

        var payment = await host.Client.PostAsJsonAsync("/api/payments", new
        {
            invoiceId = (Guid?)null,
            customerId = customerBody!.Id,
            paymentMethodId = methodBody!.Id,
            amount = 10000m,
            paidOn = "2026-08-20",
            reference = (string?)null,
            notes = "تحصيل على الحساب"
        });
        Assert.True(payment.IsSuccessStatusCode);

        var statement = await host.Client.GetFromJsonAsync<StatementResponse>($"/api/customers/{customerBody.Id}/statement", JsonOptions);
        Assert.Equal(-10000m, statement!.Outstanding);
    }

    [Fact]
    public async Task Assistant_CreatesDraftOnly_AndApproveCreatesSalesDraft()
    {
        await using var host = await TestHost.StartAsync();
        await host.Client.PostAsJsonAsync("/api/auth/setup", TestHost.SampleSetup());
        await host.Client.PostAsync("/api/auth/logout", null);
        await host.Client.PostAsJsonAsync("/api/auth/login", new { userName = "father", password = "father1" });

        await host.Client.PostAsJsonAsync("/api/customers", new
        {
            name = "نصره الشافعي",
            customerTypeId = (Guid?)null,
            contactPerson = (string?)null,
            phone = (string?)null,
            whatsApp = (string?)null,
            address = (string?)null
        });
        await host.Client.PostAsJsonAsync("/api/products", new
        {
            name = "فوايجر جولد",
            brand = "Voyager",
            category = "Oil",
            specification = "5W-40",
            isActive = true,
            imageRelativePath = (string?)null,
            variants = new[]
            {
                new
                {
                    packagingType = "كرتونة",
                    packagingSize = "1 لتر",
                    sku = "VG-1",
                    barcode = (string?)null,
                    minStock = (decimal?)null,
                    standardWholesalePrice = 5655m,
                    isActive = true
                }
            }
        });

        var parsed = await host.Client.PostAsJsonAsync("/api/assistant/parse", new
        {
            text = "اعمل فاتورة لنصره، 3 كراتين فوايجر جولد 1 لتر",
            locale = "ar"
        });
        Assert.Equal(HttpStatusCode.OK, parsed.StatusCode);
        var draft = await parsed.Content.ReadFromJsonAsync<AssistantDraftResponse>(JsonOptions);
        Assert.Equal("pending", draft!.Status);
        Assert.True(draft.CanApprove);

        var approved = await host.Client.PostAsync($"/api/assistant/drafts/{draft.DraftId}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var result = await approved.Content.ReadFromJsonAsync<AssistantApproveResponse>(JsonOptions);
        Assert.Equal("approved", result!.Status);
        Assert.NotNull(result.CreatedEntityId);

        var invoice = await host.Client.GetFromJsonAsync<InvoiceResponse>($"/api/sales/invoices/{result.CreatedEntityId}", JsonOptions);
        Assert.Equal("draft", invoice!.Status);
        Assert.Equal(3m, invoice.Lines[0].Quantity);
    }

    private sealed record IdResponse(Guid Id);
    private sealed record ProductResponse(Guid Id, VariantResponse[] Variants);
    private sealed record VariantResponse(Guid Id);
    private sealed record InvoiceResponse(
        Guid Id,
        string Status,
        decimal LinesSubtotal,
        bool HasManualTotal,
        decimal GoodsTotal,
        InvoiceLineResponse[] Lines);
    private sealed record InvoiceLineResponse(decimal Quantity, decimal? UnitPrice);
    private sealed record StatementResponse(decimal Outstanding);
    private sealed record AssistantDraftResponse(Guid DraftId, string Status, bool CanApprove);
    private sealed record AssistantApproveResponse(string Status, Guid? CreatedEntityId);
}
