using System.Text.Json;
using PetroTrans.Domain;
using PetroTrans.Domain.Identity;

namespace PetroTrans.Infrastructure;

internal static class Audits
{
    public static AuditLog Create(Guid actorUserId, string action, string entityType, Guid? entityId, object? before, object after)
    {
        return new AuditLog
        {
            Id = UuidV7.New(),
            OccurredAt = DateTime.UtcNow,
            UserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
            AfterJson = JsonSerializer.Serialize(after)
        };
    }
}
