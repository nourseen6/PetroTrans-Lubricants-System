namespace PetroTrans.Domain.Identity;

public sealed class User : global::PetroTrans.Domain.IAuditedEntity
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserNameNormalized { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Locale { get; set; } = "ar";
    public string Theme { get; set; } = "light";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;
    public Guid? OriginInstallationId { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
