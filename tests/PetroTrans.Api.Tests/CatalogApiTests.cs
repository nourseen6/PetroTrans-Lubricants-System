using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PetroTrans.Domain.Identity;
using Xunit;

namespace PetroTrans.Api.Tests;

public class CatalogApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task UnauthorizedCatalogWrites_AreDenied()
    {
        await using var host = await TestHost.StartAsync();
        var response = await host.AnonymousClient.PostAsJsonAsync("/api/customers", new
        {
            name = "عميل",
            customerTypeId = (Guid?)null,
            contactPerson = "",
            phone = "",
            whatsApp = "",
            address = ""
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CustomerCreate_AssignsUniqueCodes_AndTypeIsOptional()
    {
        await using var host = await TestHost.StartAsync();
        await host.Client.PostAsJsonAsync("/api/auth/setup", TestHost.SampleSetup());

        var first = await host.Client.PostAsJsonAsync("/api/customers", NewCustomer("شركة النور"));
        var second = await host.Client.PostAsJsonAsync("/api/customers", NewCustomer("شركة السلام"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        var one = await first.Content.ReadFromJsonAsync<CustomerResponse>(JsonOptions);
        var two = await second.Content.ReadFromJsonAsync<CustomerResponse>(JsonOptions);
        Assert.Equal("CUS-0001", one!.Code);
        Assert.Equal("CUS-0002", two!.Code);
        Assert.Null(one.CustomerTypeId);
        Assert.Equal("شركة النور", one.Name);
    }

    [Fact]
    public async Task ProductAndVariant_SkuUniqueWhenPresent_BarcodeOptionalNotUnique()
    {
        await using var host = await TestHost.StartAsync();
        await host.Client.PostAsJsonAsync("/api/auth/setup", TestHost.SampleSetup());

        var created = await host.Client.PostAsJsonAsync("/api/products", NewProduct("فوايجير جولد SN API 5W40", "SKU-1", "111"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var product = await created.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.NotNull(product);
        Assert.Single(product.Variants);
        Assert.Equal("SKU-1", product.Variants[0].Sku);
        Assert.Equal("111", product.Variants[0].Barcode);

        var duplicateSku = await host.Client.PostAsJsonAsync("/api/products", NewProduct("صنف آخر", "SKU-1", "222"));
        Assert.Equal(HttpStatusCode.BadRequest, duplicateSku.StatusCode);

        var sameBarcode = await host.Client.PostAsJsonAsync("/api/products", NewProduct("صنف ثالث", "SKU-2", "111"));
        Assert.Equal(HttpStatusCode.Created, sameBarcode.StatusCode);

        var noSku = await host.Client.PostAsJsonAsync("/api/products", NewProduct("صنف بدون SKU", null, null));
        Assert.Equal(HttpStatusCode.Created, noSku.StatusCode);
    }

    [Fact]
    public async Task AuthAndPricingOverride_RemainUnchanged_AfterCatalogUse()
    {
        await using var host = await TestHost.StartAsync();
        await host.Client.PostAsJsonAsync("/api/auth/setup", TestHost.SampleSetup());
        await host.Client.PostAsJsonAsync("/api/customers", NewCustomer("عميل"));
        await host.Client.PostAsJsonAsync("/api/products", NewProduct("صنف", "A-1", null));

        var operatorOverride = await host.Client.GetAsync("/api/secure/pricing-override");
        Assert.Equal(HttpStatusCode.Forbidden, operatorOverride.StatusCode);

        await host.Client.PostAsync("/api/auth/logout", null);
        var fatherLogin = await host.Client.PostAsJsonAsync("/api/auth/login", new { userName = "father", password = "father1" });
        Assert.Equal(HttpStatusCode.OK, fatherLogin.StatusCode);
        var father = await fatherLogin.Content.ReadFromJsonAsync<AuthUserResponse>(JsonOptions);
        Assert.Contains(PermissionCodes.PricingOverride, father!.Permissions);
        var fatherOverride = await host.Client.GetAsync("/api/secure/pricing-override");
        Assert.Equal(HttpStatusCode.OK, fatherOverride.StatusCode);
    }

    private static object NewCustomer(string name)
    {
        return new
        {
            name,
            customerTypeId = (Guid?)null,
            contactPerson = "",
            phone = "",
            whatsApp = "",
            address = ""
        };
    }

    private static object NewProduct(string name, string? sku, string? barcode)
    {
        return new
        {
            name,
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
                    sku,
                    barcode,
                    minStock = (decimal?)null,
                    standardWholesalePrice = (decimal?)null,
                    isActive = true
                }
            }
        };
    }

    private sealed record CustomerResponse(string Code, string Name, Guid? CustomerTypeId);
    private sealed record ProductResponse(string Name, VariantResponse[] Variants);
    private sealed record VariantResponse(string? Sku, string? Barcode);
    private sealed record AuthUserResponse(string[] Permissions);
}
