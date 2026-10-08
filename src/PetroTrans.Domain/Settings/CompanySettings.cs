using PetroTrans.Domain;

namespace PetroTrans.Domain.Settings;

public sealed class CompanySettings : IAuditedEntity
{
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = "Petro Trans";
    public string? LogoRelativePath { get; set; }
    public string DefaultLocale { get; set; } = "ar";
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;
}
