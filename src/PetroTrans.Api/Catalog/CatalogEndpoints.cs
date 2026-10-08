using PetroTrans.Application.Catalog;
using PetroTrans.Domain.Identity;

namespace PetroTrans.Api.Catalog;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this WebApplication app)
    {
        var customers = app.MapGroup("/api/customers").RequireAuthorization();
        customers.MapGet("", (Delegate)ListCustomersAsync).RequireAuthorization($"perm:{PermissionCodes.CustomersView}");
        customers.MapGet("/{id:guid}", (Delegate)GetCustomerAsync).RequireAuthorization($"perm:{PermissionCodes.CustomersView}");
        customers.MapPost("", (Delegate)CreateCustomerAsync).RequireAuthorization($"perm:{PermissionCodes.CustomersEdit}");
        customers.MapPut("/{id:guid}", (Delegate)UpdateCustomerAsync).RequireAuthorization($"perm:{PermissionCodes.CustomersEdit}");
        customers.MapPost("/{id:guid}/archive", (Delegate)ArchiveCustomerAsync).RequireAuthorization($"perm:{PermissionCodes.CustomersEdit}");
        customers.MapPost("/{id:guid}/restore", (Delegate)RestoreCustomerAsync).RequireAuthorization($"perm:{PermissionCodes.CustomersEdit}");

        var types = app.MapGroup("/api/customer-types").RequireAuthorization();
        types.MapGet("", (Delegate)ListTypesAsync).RequireAuthorization($"perm:{PermissionCodes.CustomersView}");
        types.MapPost("", (Delegate)CreateTypeAsync).RequireAuthorization($"perm:{PermissionCodes.SettingsManage}");
        types.MapPut("/{id:guid}", (Delegate)UpdateTypeAsync).RequireAuthorization($"perm:{PermissionCodes.SettingsManage}");

        var products = app.MapGroup("/api/products").RequireAuthorization();
        products.MapGet("", (Delegate)ListProductsAsync).RequireAuthorization($"perm:{PermissionCodes.ProductsView}");
        products.MapGet("/{id:guid}", (Delegate)GetProductAsync).RequireAuthorization($"perm:{PermissionCodes.ProductsView}");
        products.MapPost("", (Delegate)CreateProductAsync).RequireAuthorization($"perm:{PermissionCodes.ProductsEdit}");
        products.MapPut("/{id:guid}", (Delegate)UpdateProductAsync).RequireAuthorization($"perm:{PermissionCodes.ProductsEdit}");
        products.MapPost("/{id:guid}/archive", (Delegate)ArchiveProductAsync).RequireAuthorization($"perm:{PermissionCodes.ProductsEdit}");
        products.MapPost("/{id:guid}/restore", (Delegate)RestoreProductAsync).RequireAuthorization($"perm:{PermissionCodes.ProductsEdit}");
        products.MapPost("/{id:guid}/variants", (Delegate)AddVariantAsync).RequireAuthorization($"perm:{PermissionCodes.ProductsEdit}");

        app.MapPut("/api/variants/{id:guid}", (Delegate)UpdateVariantAsync)
            .RequireAuthorization($"perm:{PermissionCodes.ProductsEdit}");
        app.MapPost("/api/variants/{id:guid}/archive", (Delegate)ArchiveVariantAsync)
            .RequireAuthorization($"perm:{PermissionCodes.ProductsEdit}");
        app.MapPost("/api/variants/{id:guid}/restore", (Delegate)RestoreVariantAsync)
            .RequireAuthorization($"perm:{PermissionCodes.ProductsEdit}");
        app.MapGet("/api/assets/product-images", (Delegate)ListImagesAsync)
            .RequireAuthorization($"perm:{PermissionCodes.ProductsView}");
        app.MapGet("/api/variants", (Delegate)SearchVariantsAsync)
            .RequireAuthorization($"perm:{PermissionCodes.ProductsView}");
        app.MapGet("/api/home/summary", (Delegate)HomeSummaryAsync)
            .RequireAuthorization();

        var pricing = app.MapGroup("/api/pricing").RequireAuthorization();
        pricing.MapGet("/matrix", (Delegate)ListPricingMatrixAsync).RequireAuthorization($"perm:{PermissionCodes.PricingView}");
        pricing.MapGet("/customer-prices", (Delegate)ListCustomerPricesAsync).RequireAuthorization($"perm:{PermissionCodes.PricingView}");
        pricing.MapPost("/customer-prices", (Delegate)SetCustomerPriceAsync).RequireAuthorization($"perm:{PermissionCodes.PricingEditMasters}");
        pricing.MapDelete("/customer-prices", (Delegate)RemoveCustomerPriceAsync).RequireAuthorization($"perm:{PermissionCodes.PricingEditMasters}");
        pricing.MapPut("/base-price/{variantId:guid}", (Delegate)UpdateBasePriceAsync).RequireAuthorization($"perm:{PermissionCodes.PricingEditMasters}");
        pricing.MapPut("/purchase-price/{variantId:guid}", (Delegate)UpdatePurchasePriceAsync).RequireAuthorization($"perm:{PermissionCodes.PricingEditMasters}");
        pricing.MapGet("/history", (Delegate)GetPriceHistoryAsync).RequireAuthorization($"perm:{PermissionCodes.PricingView}");
    }

    private static async Task<IResult> ListCustomersAsync(string? q, bool? includeInactive, ICustomerService customers, CancellationToken cancellationToken)
    {
        return Results.Json(await customers.ListAsync(q, includeInactive == true, cancellationToken));
    }

    private static async Task<IResult> GetCustomerAsync(Guid id, ICustomerService customers, CancellationToken cancellationToken)
    {
        var item = await customers.GetAsync(id, cancellationToken);
        return item is null
            ? Results.Json(new { error = "العميل غير موجود." }, statusCode: StatusCodes.Status404NotFound)
            : Results.Json(item);
    }

    private static async Task<IResult> CreateCustomerAsync(SaveCustomerRequest request, HttpContext http, ICustomerService customers, CancellationToken cancellationToken)
    {
        return From(await customers.CreateAsync(request, CurrentUser.RequireId(http), cancellationToken), StatusCodes.Status201Created);
    }

    private static async Task<IResult> UpdateCustomerAsync(Guid id, SaveCustomerRequest request, HttpContext http, ICustomerService customers, CancellationToken cancellationToken)
    {
        return From(await customers.UpdateAsync(id, request, CurrentUser.RequireId(http), cancellationToken));
    }

    private static async Task<IResult> ArchiveCustomerAsync(Guid id, HttpContext http, ICustomerService customers, CancellationToken cancellationToken)
    {
        return From(await customers.ArchiveAsync(id, CurrentUser.RequireId(http), cancellationToken));
    }

    private static async Task<IResult> RestoreCustomerAsync(Guid id, HttpContext http, ICustomerService customers, CancellationToken cancellationToken)
    {
        return From(await customers.RestoreAsync(id, CurrentUser.RequireId(http), cancellationToken));
    }

    private static async Task<IResult> ListTypesAsync(bool activeOnly, ICustomerTypeService types, CancellationToken cancellationToken)
    {
        return Results.Json(await types.ListAsync(activeOnly, cancellationToken));
    }

    private static async Task<IResult> CreateTypeAsync(SaveCustomerTypeRequest request, HttpContext http, ICustomerTypeService types, CancellationToken cancellationToken)
    {
        return From(await types.CreateAsync(request, CurrentUser.RequireId(http), cancellationToken), StatusCodes.Status201Created);
    }

    private static async Task<IResult> UpdateTypeAsync(Guid id, SaveCustomerTypeRequest request, HttpContext http, ICustomerTypeService types, CancellationToken cancellationToken)
    {
        return From(await types.UpdateAsync(id, request, CurrentUser.RequireId(http), cancellationToken));
    }

    private static async Task<IResult> ListProductsAsync(string? q, bool? includeInactive, ICatalogService catalog, CancellationToken cancellationToken)
    {
        return Results.Json(await catalog.ListProductsAsync(q, includeInactive == true, cancellationToken));
    }

    private static async Task<IResult> GetProductAsync(Guid id, ICatalogService catalog, CancellationToken cancellationToken)
    {
        var item = await catalog.GetProductAsync(id, cancellationToken);
        return item is null
            ? Results.Json(new { error = "الصنف غير موجود." }, statusCode: StatusCodes.Status404NotFound)
            : Results.Json(item);
    }

    private static async Task<IResult> CreateProductAsync(CreateProductRequest request, HttpContext http, ICatalogService catalog, CancellationToken cancellationToken)
    {
        return From(await catalog.CreateProductAsync(request, CurrentUser.RequireId(http), cancellationToken), StatusCodes.Status201Created);
    }

    private static async Task<IResult> UpdateProductAsync(Guid id, UpdateProductRequest request, HttpContext http, ICatalogService catalog, CancellationToken cancellationToken)
    {
        return From(await catalog.UpdateProductAsync(id, request, CurrentUser.RequireId(http), cancellationToken));
    }

    private static async Task<IResult> ArchiveProductAsync(Guid id, HttpContext http, ICatalogService catalog, CancellationToken cancellationToken)
    {
        return From(await catalog.ArchiveProductAsync(id, CurrentUser.RequireId(http), cancellationToken));
    }

    private static async Task<IResult> RestoreProductAsync(Guid id, HttpContext http, ICatalogService catalog, CancellationToken cancellationToken)
    {
        return From(await catalog.RestoreProductAsync(id, CurrentUser.RequireId(http), cancellationToken));
    }

    private static async Task<IResult> AddVariantAsync(Guid id, SaveVariantRequest request, HttpContext http, ICatalogService catalog, CancellationToken cancellationToken)
    {
        return From(await catalog.AddVariantAsync(id, request, CurrentUser.RequireId(http), cancellationToken), StatusCodes.Status201Created);
    }

    private static async Task<IResult> UpdateVariantAsync(Guid id, SaveVariantRequest request, HttpContext http, ICatalogService catalog, CancellationToken cancellationToken)
    {
        return From(await catalog.UpdateVariantAsync(id, request, CurrentUser.RequireId(http), cancellationToken));
    }

    private static async Task<IResult> ArchiveVariantAsync(Guid id, HttpContext http, ICatalogService catalog, CancellationToken cancellationToken)
    {
        return From(await catalog.ArchiveVariantAsync(id, CurrentUser.RequireId(http), cancellationToken));
    }

    private static async Task<IResult> RestoreVariantAsync(Guid id, HttpContext http, ICatalogService catalog, CancellationToken cancellationToken)
    {
        return From(await catalog.RestoreVariantAsync(id, CurrentUser.RequireId(http), cancellationToken));
    }

    private static async Task<IResult> ListImagesAsync(ICatalogService catalog)
    {
        return Results.Json(await catalog.ListOfficialImagesAsync());
    }

    private static async Task<IResult> SearchVariantsAsync(string? q, Guid? customerId, ICatalogService catalog, CancellationToken cancellationToken)
    {
        return Results.Json(await catalog.SearchVariantsAsync(q, customerId, cancellationToken));
    }

    private static async Task<IResult> HomeSummaryAsync(HttpContext http, IHomeService home, CancellationToken cancellationToken)
    {
        var summary = await home.GetSummaryAsync(CurrentUser.DisplayName(http), CurrentUser.UserName(http), cancellationToken);
        return Results.Json(summary);
    }

    private static async Task<IResult> ListPricingMatrixAsync(string? q, IPricingService pricing, CancellationToken cancellationToken)
        => Results.Json(await pricing.ListMatrixAsync(q, cancellationToken));

    private static async Task<IResult> ListCustomerPricesAsync(Guid? customerId, IPricingService pricing, CancellationToken cancellationToken)
        => Results.Json(await pricing.ListCustomerPricesAsync(customerId, cancellationToken));

    private static async Task<IResult> SetCustomerPriceAsync(SetCustomerPriceRequest request, HttpContext http, IPricingService pricing, CancellationToken cancellationToken)
        => From(await pricing.SetCustomerPriceAsync(request.CustomerId, request.VariantId, request.UnitPrice, request.Reason, CurrentUser.RequireId(http), cancellationToken), StatusCodes.Status201Created);

    private static async Task<IResult> RemoveCustomerPriceAsync(Guid customerId, Guid variantId, string? reason, HttpContext http, IPricingService pricing, CancellationToken cancellationToken)
        => From(await pricing.RemoveCustomerPriceAsync(customerId, variantId, reason, CurrentUser.RequireId(http), cancellationToken));

    private static async Task<IResult> UpdateBasePriceAsync(Guid variantId, UpdateBasePriceRequest request, HttpContext http, IPricingService pricing, CancellationToken cancellationToken)
        => From(await pricing.UpdateBasePriceAsync(variantId, request.UnitPrice, request.Reason, CurrentUser.RequireId(http), cancellationToken));

    private static async Task<IResult> UpdatePurchasePriceAsync(Guid variantId, UpdateBasePriceRequest request, HttpContext http, IPricingService pricing, CancellationToken cancellationToken)
        => From(await pricing.UpdatePurchasePriceAsync(variantId, request.UnitPrice, request.Reason, CurrentUser.RequireId(http), cancellationToken));

    private static async Task<IResult> GetPriceHistoryAsync(Guid? variantId, Guid? customerId, IPricingService pricing, CancellationToken cancellationToken)
        => Results.Json(await pricing.GetHistoryAsync(variantId, customerId, cancellationToken));

    private static IResult From<T>(OperationResult<T> result, int successStatus = StatusCodes.Status200OK)
    {
        if (!result.Succeeded || result.Value is null)
        {
            return Results.Json(new { error = result.Error }, statusCode: result.ErrorStatus);
        }

        return successStatus == StatusCodes.Status200OK
            ? Results.Json(result.Value)
            : Results.Json(result.Value, statusCode: successStatus);
    }
}

public sealed record SetCustomerPriceRequest(Guid CustomerId, Guid VariantId, decimal UnitPrice, string? Reason);
public sealed record UpdateBasePriceRequest(decimal UnitPrice, string? Reason);
