using PetroTrans.Domain;

namespace PetroTrans.Domain.Settings;

public sealed class NumberSeries : IAuditedEntity
{
    public Guid Id { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public int Padding { get; set; }
    public int NextValue { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;
}
