using Microsoft.EntityFrameworkCore;
using PetroTrans.Domain;
using PetroTrans.Domain.Identity;

namespace PetroTrans.Infrastructure.Persistence;

public sealed partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserName).HasColumnName("username").HasMaxLength(64).IsRequired();
            entity.Property(x => x.UserNameNormalized).HasColumnName("username_normalized").HasMaxLength(64).IsRequired();
            entity.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(512).IsRequired();
            entity.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(128).IsRequired();
            entity.Property(x => x.Locale).HasColumnName("locale").HasMaxLength(8).IsRequired();
            entity.Property(x => x.Theme).HasColumnName("theme").HasMaxLength(16).IsRequired();
            entity.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");
            entity.Property(x => x.RowVersion).HasColumnName("row_version").IsRequired();
            entity.Property(x => x.OriginInstallationId).HasColumnName("origin_installation_id");
            entity.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            entity.Property(x => x.DeletedByUserId).HasColumnName("deleted_by_user_id");
            entity.HasIndex(x => x.UserNameNormalized)
                .IsUnique()
                .HasDatabaseName("ix_users_username_normalized")
                .HasFilter("deleted_at IS NULL");
            entity.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(64).IsRequired();
            entity.Property(x => x.NameAr).HasColumnName("name_ar").HasMaxLength(128).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.Property(x => x.RowVersion).HasColumnName("row_version").IsRequired();
            entity.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ix_roles_code");
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("permissions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(64).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.Property(x => x.RowVersion).HasColumnName("row_version").IsRequired();
            entity.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ix_permissions_code");
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("user_roles");
            entity.HasKey(x => new { x.UserId, x.RoleId });
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.RoleId).HasColumnName("role_id");
            entity.HasOne(x => x.User)
                .WithMany(x => x.UserRoles)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Role)
                .WithMany(x => x.UserRoles)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => x.User.DeletedAt == null);
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("role_permissions");
            entity.HasKey(x => new { x.RoleId, x.PermissionId });
            entity.Property(x => x.RoleId).HasColumnName("role_id");
            entity.Property(x => x.PermissionId).HasColumnName("permission_id");
            entity.HasOne(x => x.Role)
                .WithMany(x => x.RolePermissions)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Permission)
                .WithMany(x => x.RolePermissions)
                .HasForeignKey(x => x.PermissionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_log");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OccurredAt).HasColumnName("occurred_at").IsRequired();
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.Action).HasColumnName("action").HasMaxLength(128).IsRequired();
            entity.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(64);
            entity.Property(x => x.EntityId).HasColumnName("entity_id");
            entity.Property(x => x.BeforeJson).HasColumnName("before_json");
            entity.Property(x => x.AfterJson).HasColumnName("after_json");
            entity.Property(x => x.CorrelationId).HasColumnName("correlation_id");
            entity.Property(x => x.InstallationId).HasColumnName("installation_id");
            entity.HasIndex(x => x.OccurredAt).HasDatabaseName("ix_audit_log_occurred_at");
        });

        ConfigureCatalog(modelBuilder);
        ConfigureSales(modelBuilder);
        ConfigureOperations(modelBuilder);
    }

    private void StampAuditFields()
    {
        var utc = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<IAuditedEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAt == default)
                {
                    entry.Entity.CreatedAt = utc;
                }

                entry.Entity.UpdatedAt = utc;
                if (entry.Entity.RowVersion <= 0)
                {
                    entry.Entity.RowVersion = 1;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = utc;
                entry.Entity.RowVersion++;
            }
        }
    }
}
