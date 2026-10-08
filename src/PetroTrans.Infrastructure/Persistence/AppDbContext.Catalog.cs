using Microsoft.EntityFrameworkCore;
using PetroTrans.Domain;
using PetroTrans.Domain.Catalog;
using PetroTrans.Domain.Identity;
using PetroTrans.Domain.Parties;
using PetroTrans.Domain.Settings;

namespace PetroTrans.Infrastructure.Persistence;

public sealed partial class AppDbContext
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerType> CustomerTypes => Set<CustomerType>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductMedia> ProductMedia => Set<ProductMedia>();
    public DbSet<CustomerVariantPrice> CustomerVariantPrices => Set<CustomerVariantPrice>();
    public DbSet<CustomerVariantPriceHistory> CustomerVariantPriceHistories => Set<CustomerVariantPriceHistory>();
    public DbSet<NumberSeries> NumberSeries => Set<NumberSeries>();

    private void ConfigureCatalog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomerType>(entity =>
        {
            entity.ToTable("customer_types");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
            entity.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            MapAudit(entity);
            entity.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            entity.Property(x => x.DeletedByUserId).HasColumnName("deleted_by_user_id");
            entity.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(16).IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(256).IsRequired();
            entity.Property(x => x.CustomerTypeId).HasColumnName("customer_type_id");
            entity.Property(x => x.ContactPerson).HasColumnName("contact_person").HasMaxLength(128);
            entity.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(64);
            entity.Property(x => x.WhatsApp).HasColumnName("whatsapp").HasMaxLength(64);
            entity.Property(x => x.Address).HasColumnName("address").HasMaxLength(512);
            entity.Property(x => x.CreditLimit).HasColumnName("credit_limit").HasPrecision(18, 4);
            entity.Property(x => x.PaymentTermDays).HasColumnName("payment_term_days");
            entity.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            entity.Property(x => x.OriginInstallationId).HasColumnName("origin_installation_id");
            MapAudit(entity);
            entity.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            entity.Property(x => x.DeletedByUserId).HasColumnName("deleted_by_user_id");
            entity.HasIndex(x => x.Code)
                .IsUnique()
                .HasDatabaseName("ix_customers_code")
                .HasFilter("deleted_at IS NULL");
            entity.HasOne(x => x.CustomerType)
                .WithMany(x => x.Customers)
                .HasForeignKey(x => x.CustomerTypeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(256).IsRequired();
            entity.Property(x => x.Brand).HasColumnName("brand").HasMaxLength(128);
            entity.Property(x => x.Category).HasColumnName("category").HasMaxLength(128);
            entity.Property(x => x.Specification).HasColumnName("specification").HasMaxLength(256);
            entity.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            entity.Property(x => x.OriginInstallationId).HasColumnName("origin_installation_id");
            MapAudit(entity);
            entity.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            entity.Property(x => x.DeletedByUserId).HasColumnName("deleted_by_user_id");
            entity.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<ProductVariant>(entity =>
        {
            entity.ToTable("product_variants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
            entity.Property(x => x.PackagingType).HasColumnName("packaging_type").HasMaxLength(64).IsRequired();
            entity.Property(x => x.PackagingSize).HasColumnName("packaging_size").HasMaxLength(64).IsRequired();
            entity.Property(x => x.Sku).HasColumnName("sku").HasMaxLength(64);
            entity.Property(x => x.Barcode).HasColumnName("barcode").HasMaxLength(64);
            entity.Property(x => x.MinStock).HasColumnName("min_stock").HasPrecision(18, 4);
            entity.Property(x => x.StandardWholesalePrice).HasColumnName("standard_wholesale_price").HasPrecision(18, 4);
            entity.Property(x => x.StandardPurchasePrice).HasColumnName("standard_purchase_price").HasPrecision(18, 4);
            entity.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            entity.Property(x => x.OriginInstallationId).HasColumnName("origin_installation_id");
            MapAudit(entity);
            entity.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            entity.Property(x => x.DeletedByUserId).HasColumnName("deleted_by_user_id");
            entity.HasIndex(x => x.Sku)
                .IsUnique()
                .HasDatabaseName("ix_product_variants_sku")
                .HasFilter("sku IS NOT NULL AND deleted_at IS NULL");
            entity.HasOne(x => x.Product)
                .WithMany(x => x.Variants)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<CustomerVariantPrice>(entity =>
        {
            entity.ToTable("customer_variant_prices");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
            entity.Property(x => x.VariantId).HasColumnName("variant_id").IsRequired();
            entity.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 4).IsRequired();
            MapAudit(entity);
            entity.HasIndex(x => new { x.CustomerId, x.VariantId })
                .IsUnique()
                .HasDatabaseName("ix_customer_variant_prices_customer_variant");
            entity.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Variant)
                .WithMany()
                .HasForeignKey(x => x.VariantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CustomerVariantPriceHistory>(entity =>
        {
            entity.ToTable("customer_variant_price_history");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CustomerVariantPriceId).HasColumnName("customer_variant_price_id");
            entity.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
            entity.Property(x => x.VariantId).HasColumnName("variant_id").IsRequired();
            entity.Property(x => x.OldUnitPrice).HasColumnName("old_unit_price").HasPrecision(18, 4);
            entity.Property(x => x.NewUnitPrice).HasColumnName("new_unit_price").HasPrecision(18, 4);
            entity.Property(x => x.ChangeKind).HasColumnName("change_kind").HasMaxLength(16).IsRequired();
            entity.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(512);
            entity.Property(x => x.ChangedAt).HasColumnName("changed_at").IsRequired();
            entity.Property(x => x.ChangedByUserId).HasColumnName("changed_by_user_id");
            entity.HasIndex(x => x.VariantId).HasDatabaseName("ix_customer_variant_price_history_variant");
            entity.HasIndex(x => x.CustomerId).HasDatabaseName("ix_customer_variant_price_history_customer");
            entity.HasIndex(x => x.ChangedAt).HasDatabaseName("ix_customer_variant_price_history_changed_at");
        });

        modelBuilder.Entity<ProductMedia>(entity =>
        {
            entity.ToTable("product_media");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProductId).HasColumnName("product_id");
            entity.Property(x => x.VariantId).HasColumnName("variant_id");
            entity.Property(x => x.RelativePath).HasColumnName("relative_path").HasMaxLength(512).IsRequired();
            MapAudit(entity);
            entity.HasOne(x => x.Product)
                .WithMany(x => x.Media)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Variant)
                .WithMany(x => x.Media)
                .HasForeignKey(x => x.VariantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NumberSeries>(entity =>
        {
            entity.ToTable("number_series");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DocumentType).HasColumnName("document_type").HasMaxLength(32).IsRequired();
            entity.Property(x => x.Prefix).HasColumnName("prefix").HasMaxLength(16).IsRequired();
            entity.Property(x => x.Padding).HasColumnName("padding").IsRequired();
            entity.Property(x => x.NextValue).HasColumnName("next_value").IsRequired();
            MapAudit(entity);
            entity.HasIndex(x => x.DocumentType).IsUnique().HasDatabaseName("ix_number_series_document_type");
        });
    }

    private static void MapAudit<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> entity)
        where T : class, IAuditedEntity
    {
        entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        entity.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        entity.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");
        entity.Property(x => x.RowVersion).HasColumnName("row_version").IsRequired();
    }
}
