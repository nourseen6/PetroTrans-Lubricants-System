using PetroTrans.Application.Assistant;
using PetroTrans.Application.Catalog;
using PetroTrans.Application.Identity;
using PetroTrans.Domain.Identity;

namespace PetroTrans.Api.Assistant;

public static class AssistantEndpoints
{
    public static void MapAssistantEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/assistant").RequireAuthorization($"perm:{PermissionCodes.AssistantUse}");
        group.MapPost("/parse", (Delegate)ParseAsync);
        group.MapGet("/drafts/{id:guid}", (Delegate)GetDraftAsync);
        group.MapPost("/drafts/{id:guid}/approve", (Delegate)ApproveAsync);
        group.MapPost("/drafts/{id:guid}/reject", (Delegate)RejectAsync);
    }

    private static async Task<IResult> ParseAsync(AssistantParseRequest request, HttpContext http, IAssistantService assistant, CancellationToken cancellationToken)
    {
        return From(await assistant.ParseAsync(request, CurrentUser.RequireId(http), cancellationToken));
    }

    private static async Task<IResult> GetDraftAsync(Guid id, IAssistantService assistant, CancellationToken cancellationToken)
    {
        var item = await assistant.GetDraftAsync(id, cancellationToken);
        return item is null
            ? Results.Json(new { error = "المسودة غير موجودة." }, statusCode: StatusCodes.Status404NotFound)
            : Results.Json(item);
    }

    private static async Task<IResult> ApproveAsync(Guid id, HttpContext http, IAssistantService assistant, IAuthService auth, CancellationToken cancellationToken)
    {
        var userId = CurrentUser.RequireId(http);
        var canOverride = await auth.HasPermissionAsync(userId, PermissionCodes.PricingOverride, cancellationToken);
        return From(await assistant.ApproveAsync(id, userId, canOverride, cancellationToken));
    }

    private static async Task<IResult> RejectAsync(Guid id, HttpContext http, IAssistantService assistant, CancellationToken cancellationToken)
    {
        return From(await assistant.RejectAsync(id, CurrentUser.RequireId(http), cancellationToken));
    }

    private static IResult From<T>(OperationResult<T> result)
    {
        if (!result.Succeeded || result.Value is null)
        {
            return Results.Json(new { error = result.Error }, statusCode: result.ErrorStatus);
        }

        return Results.Json(result.Value);
    }
}
