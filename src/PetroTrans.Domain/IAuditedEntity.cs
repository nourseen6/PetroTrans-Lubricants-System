namespace PetroTrans.Domain;

public interface IAuditedEntity
{
    DateTime CreatedAt { get; set; }
    Guid? CreatedByUserId { get; set; }
    DateTime UpdatedAt { get; set; }
    Guid? UpdatedByUserId { get; set; }
    int RowVersion { get; set; }
}
