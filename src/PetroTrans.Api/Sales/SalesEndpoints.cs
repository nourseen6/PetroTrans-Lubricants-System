using PetroTrans.Application.Catalog;
using PetroTrans.Application.Identity;
using PetroTrans.Application.Sales;
using PetroTrans.Domain.Identity;

namespace PetroTrans.Api.Sales;

public static class SalesEndpoints
{
    public static void MapSalesEndpoints(this WebApplication app)
    {
        var invoices = app.MapGroup("/api/sales/invoices").RequireAuthorization();
        invoices.MapGet("", (Delegate)ListAsync).RequireAuthorization($"perm:{PermissionCodes.SalesView}");
        invoices.MapGet("/{id:guid}", (Delegate)GetAsync).RequireAuthorization($"perm:{PermissionCodes.SalesView}");
        invoices.MapPost("", (Delegate)CreateAsync).RequireAuthorization($"perm:{PermissionCodes.SalesCreate}");
        invoices.MapPut("/{id:guid}", (Delegate)UpdateAsync).RequireAuthorization($"perm:{PermissionCodes.SalesCreate}");
        invoices.MapPost("/{id:guid}/post", (Delegate)PostAsync).RequireAuthorization($"perm:{PermissionCodes.SalesPost}");
        invoices.MapPost("/{id:guid}/unpost", (Delegate)UnpostAsync).RequireAuthorization($"perm:{PermissionCodes.SalesEditPosted}");
        invoices.MapDelete("/{id:guid}", (Delegate)DeleteAsync).RequireAuthorization($"perm:{PermissionCodes.SalesCreate}");
    }

    private static async Task<IResult> ListAsync(string? q, Guid? customerId, ISalesInvoiceService sales, CancellationToken cancellationToken)
    {
        return Results.Json(await sales.ListAsync(q, customerId, cancellationToken));
    }

    private static async Task<IResult> GetAsync(Guid id, ISalesInvoiceService sales, CancellationToken cancellationToken)
    {
        var item = await sales.GetAsync(id, cancellationToken);
        return item is null
            ? Results.Json(new { error = "الفاتورة غير موجودة." }, statusCode: StatusCodes.Status404NotFound)
            : Results.Json(item);
    }

    private static async Task<IResult> CreateAsync(
        SaveInvoiceRequest request,
        HttpContext http,
        ISalesInvoiceService sales,
        IAuthService auth,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.RequireId(http);
        var canOverride = await auth.HasPermissionAsync(userId, PermissionCodes.PricingOverride, cancellationToken);
        return From(await sales.CreateAsync(request, userId, canOverride, cancellationToken), StatusCodes.Status201Created);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        SaveInvoiceRequest request,
        HttpContext http,
        ISalesInvoiceService sales,
        IAuthService auth,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.RequireId(http);
        var canOverride = await auth.HasPermissionAsync(userId, PermissionCodes.PricingOverride, cancellationToken);
        return From(await sales.UpdateAsync(id, request, userId, canOverride, cancellationToken));
    }

    private static async Task<IResult> PostAsync(Guid id, HttpContext http, ISalesInvoiceService sales, CancellationToken cancellationToken)
    {
        return From(await sales.PostAsync(id, CurrentUser.RequireId(http), cancellationToken));
    }

    private static async Task<IResult> UnpostAsync(Guid id, HttpContext http, ISalesInvoiceService sales, CancellationToken cancellationToken)
    {
        return From(await sales.UnpostAsync(id, CurrentUser.RequireId(http), cancellationToken));
    }

    private static async Task<IResult> DeleteAsync(Guid id, HttpContext http, ISalesInvoiceService sales, IAuthService auth, CancellationToken cancellationToken)
    {
        var userId = CurrentUser.RequireId(http);
        var invoice = await sales.GetAsync(id, cancellationToken);
        if (invoice is null)
        {
            return Results.Json(new { error = "الفاتورة غير موجودة." }, statusCode: StatusCodes.Status404NotFound);
        }

        if (invoice.Status == "posted")
        {
            var canEditPosted = await auth.HasPermissionAsync(userId, PermissionCodes.SalesEditPosted, cancellationToken);
            if (!canEditPosted)
            {
                return Results.Json(new { error = "لا صلاحية لحذف فاتورة مرحّلة." }, statusCode: StatusCodes.Status403Forbidden);
            }
        }

        return From(await sales.DeleteAsync(id, userId, cancellationToken));
    }

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
