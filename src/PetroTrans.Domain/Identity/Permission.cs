namespace PetroTrans.Domain.Identity;

public sealed class Permission
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int RowVersion { get; set; } = 1;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
