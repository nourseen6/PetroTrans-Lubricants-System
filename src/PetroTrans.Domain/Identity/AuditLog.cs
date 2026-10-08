namespace PetroTrans.Domain.Identity;

public sealed class AuditLog
{
    public Guid Id { get; set; }
    public DateTime OccurredAt { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public Guid? CorrelationId { get; set; }
    public Guid? InstallationId { get; set; }
}
