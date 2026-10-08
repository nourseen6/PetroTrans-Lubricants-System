using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetroTrans.Infrastructure.Persistence;
using Xunit;

namespace PetroTrans.Api.Tests;

public class SalesApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task UnauthorizedInvoiceWrites_AreDenied()
    {
        await using var host = await TestHost.StartAsync();
        var response = await host.AnonymousClient.PostAsJsonAsync("/api/sales/invoices", new
        {
            customerId = Guid.NewGuid(),
            invoiceDate = "2026-08-16",
            dueDate = (string?)null,
            notes = (string?)null,
            lines = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DraftInvoice_SavesWithoutNumber_PaidZero_AndUsesStandardPrice()
    {
        await using var host = await TestHost.StartAsync();
        await host.Client.PostAsJsonAsync("/api/auth/setup", TestHost.SampleSetup());
        var (customerId, variantId) = await SeedCustomerAndVariantAsync(host, 12.5m);

        var created = await host.Client.PostAsJsonAsync("/api/sales/invoices", NewDraft(customerId, variantId, quantity: 2, unitPrice: null));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var invoice = await created.Content.ReadFromJsonAsync<InvoiceResponse>(JsonOptions);
        Assert.NotNull(invoice);
        Assert.Null(invoice.Number);
        Assert.Equal("draft", invoice.Status);
        Assert.Equal("unpaid", invoice.PaymentStatus);
        Assert.Equal(0m, invoice.PaidTotal);
        Assert.Equal(25m, invoice.GoodsTotal);
        Assert.Equal(25m, invoice.RemainingTotal);
        Assert.Equal("standard", invoice.Lines[0].PriceSource);
        Assert.Equal(12.5m, invoice.Lines[0].UnitPrice);

        var variants = await host.Client.GetAsync("/api/variants");
        Assert.Equal(HttpStatusCode.OK, variants.StatusCode);

        var post = await host.Client.PostAsJsonAsync($"/api/sales/invoices/{invoice.Id}/post", new { });
        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
        var postBody = await post.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.Equal("أضف مخزناً من الإعدادات", postBody!.Error);
        Assert.Null((await host.Client.GetFromJsonAsync<InvoiceResponse>($"/api/sales/invoices/{invoice.Id}", JsonOptions))!.Number);

        var home = await host.Client.GetFromJsonAsync<HomeSummaryResponse>("/api/home/summary", JsonOptions);
        Assert.Equal(1, home!.DraftInvoiceCount);
    }

    [Fact]
    public async Task EmptyCustomerId_ReturnsArabicBadRequest_NotUnexpectedError()
    {
        await using var host = await TestHost.StartAsync();
        await host.Client.PostAsJsonAsync("/api/auth/setup", TestHost.SampleSetup());
        var (_, variantId) = await SeedCustomerAndVariantAsync(host, 12.5m);

        using var content = new StringContent(
            $$"""{"customerId":"","invoiceDate":"2026-08-16","dueDate":null,"notes":null,"lines":[{"variantId":"{{variantId}}","quantity":1,"unitPrice":12.5,"overrideReason":null}]}""",
            Encoding.UTF8,
            "application/json");
        var response = await host.Client.PostAsync("/api/sales/invoices", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.Equal("اختر العميل من القائمة.", body!.Error);
        Assert.DoesNotContain("غير متوقع", body.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DraftInvoice_CanSaveWithoutPrice_AndDoesNotInventOne()
    {
        await using var host = await TestHost.StartAsync();
        await host.Client.PostAsJsonAsync("/api/auth/setup", TestHost.SampleSetup());
        var (customerId, variantId) = await SeedCustomerAndVariantAsync(host, null);

        var created = await host.Client.PostAsJsonAsync("/api/sales/invoices", NewDraft(customerId, variantId, 1, null));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var invoice = await created.Content.ReadFromJsonAsync<InvoiceResponse>(JsonOptions);
        Assert.Null(invoice!.Lines[0].UnitPrice);
        Assert.Null(invoice.Lines[0].PriceSource);
        Assert.Equal(0m, invoice.GoodsTotal);
        Assert.Equal(0m, invoice.PaidTotal);
        Assert.Equal(0m, invoice.RemainingTotal);
    }

    [Fact]
    public async Task OperatorCannotOverridePrice_FatherCan_AndOverrideIsAudited()
    {
        await using var host = await TestHost.StartAsync();
        await host.Client.PostAsJsonAsync("/api/auth/setup", TestHost.SampleSetup());
        var (customerId, variantId) = await SeedCustomerAndVariantAsync(host, 10m);

        var denied = await host.Client.PostAsJsonAsync("/api/sales/invoices", NewDraft(customerId, variantId, 1, 15m));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var deniedBody = await denied.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.Equal("غير مسموح بتغيير السعر.", deniedBody!.Error);

        await host.Client.PostAsync("/api/auth/logout", null);
        var fatherLogin = await host.Client.PostAsJsonAsync("/api/auth/login", new { userName = "father", password = "father1" });
        Assert.Equal(HttpStatusCode.OK, fatherLogin.StatusCode);

        var created = await host.Client.PostAsJsonAsync("/api/sales/invoices", NewDraft(customerId, variantId, 1, 15m, "سعر خاص"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var invoice = await created.Content.ReadFromJsonAsync<InvoiceResponse>(JsonOptions);
        Assert.Equal("manual_override", invoice!.Lines[0].PriceSource);
        Assert.Equal(15m, invoice.Lines[0].UnitPrice);
        Assert.Equal(10m, invoice.Lines[0].ResolvedUnitPrice);
        Assert.Equal(15m, invoice.GoodsTotal);
        Assert.Null(invoice.Number);

        using var scope = host.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var overrideAudits = await db.AuditLogs.Where(x => x.Action == "pricing.override").ToListAsync();
        Assert.Single(overrideAudits);
        Assert.Contains("15", overrideAudits[0].AfterJson, StringComparison.Ordinal);
    }

    private static async Task<(Guid CustomerId, Guid VariantId)> SeedCustomerAndVariantAsync(TestHost host, decimal? price)
    {
        var customerResponse = await host.Client.PostAsJsonAsync("/api/customers", new
        {
            name = "عميل تجريبي",
            customerTypeId = (Guid?)null,
            contactPerson = "",
            phone = "",
            whatsApp = "",
            address = ""
        });
        var customer = await customerResponse.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        var productResponse = await host.Client.PostAsJsonAsync("/api/products", new
        {
            name = "صنف تجريبي",
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
                    standardWholesalePrice = price,
                    isActive = true
                }
            }
        });
        var product = await productResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        return (customer!.Id, product!.Variants[0].Id);
    }

    private static object NewDraft(Guid customerId, Guid variantId, decimal quantity, decimal? unitPrice, string? reason = null)
    {
        return new
        {
            customerId,
            invoiceDate = "2026-08-16",
            dueDate = (string?)null,
            notes = (string?)null,
            lines = new[]
            {
                new
                {
                    variantId,
                    quantity,
                    unitPrice,
                    overrideReason = reason
                }
            }
        };
    }

    private sealed record IdResponse(Guid Id);
    private sealed record ProductResponse(Guid Id, VariantResponse[] Variants);
    private sealed record VariantResponse(Guid Id);
    private sealed record InvoiceResponse(
        Guid Id,
        string? Number,
        string Status,
        decimal GoodsTotal,
        decimal PaidTotal,
        decimal RemainingTotal,
        string PaymentStatus,
        InvoiceLineResponse[] Lines);
    private sealed record InvoiceLineResponse(
        decimal Quantity,
        decimal? UnitPrice,
        decimal? LineTotal,
        string? PriceSource,
        decimal? ResolvedUnitPrice);
    private sealed record HomeSummaryResponse(int DraftInvoiceCount);
    private sealed record ErrorResponse(string Error);
}
