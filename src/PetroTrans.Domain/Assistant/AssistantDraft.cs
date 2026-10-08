using PetroTrans.Domain;

namespace PetroTrans.Domain.Assistant;

public static class AssistantDraftStatuses
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
}

public sealed class AssistantDraft : IAuditedEntity
{
    public Guid Id { get; set; }
    public string IntentType { get; set; } = string.Empty;
    public string Status { get; set; } = AssistantDraftStatuses.Pending;
    public string UserText { get; set; } = string.Empty;
    public string DraftJson { get; set; } = "{}";
    public string? ResultJson { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;
}
