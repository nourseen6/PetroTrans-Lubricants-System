using Microsoft.EntityFrameworkCore;
using PetroTrans.Domain.Sales;

namespace PetroTrans.Infrastructure.Persistence;

public sealed partial class AppDbContext
{
    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
    public DbSet<SalesInvoiceLine> SalesInvoiceLines => Set<SalesInvoiceLine>();

    private void ConfigureSales(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SalesInvoice>(entity =>
        {
            entity.ToTable("sales_invoices");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Number).HasColumnName("number").HasMaxLength(32);
            entity.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
            entity.Property(x => x.WarehouseId).HasColumnName("warehouse_id");
            entity.Property(x => x.InvoiceDate).HasColumnName("invoice_date").IsRequired();
            entity.Property(x => x.DueDate).HasColumnName("due_date");
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.GoodsTotal).HasColumnName("goods_total").HasPrecision(18, 4).IsRequired();
            entity.Property(x => x.LinesSubtotal).HasColumnName("lines_subtotal").HasPrecision(18, 4).IsRequired();
            entity.Property(x => x.DiscountAmount).HasColumnName("discount_amount").HasPrecision(18, 4).IsRequired();
            entity.Property(x => x.ManualAdjustment).HasColumnName("manual_adjustment").HasPrecision(18, 4).IsRequired();
            entity.Property(x => x.HasManualTotal).HasColumnName("has_manual_total").IsRequired();
            entity.Property(x => x.ManualTotalReason).HasColumnName("manual_total_reason").HasMaxLength(512);
            entity.Property(x => x.PaidTotal).HasColumnName("paid_total").HasPrecision(18, 4).IsRequired();
            entity.Property(x => x.RemainingTotal).HasColumnName("remaining_total").HasPrecision(18, 4).IsRequired();
            entity.Property(x => x.PaymentStatus).HasColumnName("payment_status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(1024);
            entity.Property(x => x.OriginInstallationId).HasColumnName("origin_installation_id");
            MapAudit(entity);
            entity.HasIndex(x => x.Number)
                .IsUnique()
                .HasDatabaseName("ix_sales_invoices_number")
                .HasFilter("number IS NOT NULL");
            entity.HasIndex(x => new { x.Status, x.InvoiceDate }).HasDatabaseName("ix_sales_invoices_status_date");
            entity.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Warehouse)
                .WithMany()
                .HasForeignKey(x => x.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SalesInvoiceLine>(entity =>
        {
            entity.ToTable("sales_invoice_lines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.InvoiceId).HasColumnName("invoice_id").IsRequired();
            entity.Property(x => x.LineNumber).HasColumnName("line_number").IsRequired();
            entity.Property(x => x.VariantId).HasColumnName("variant_id").IsRequired();
            entity.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(18, 4).IsRequired();
            entity.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 4);
            entity.Property(x => x.LineTotal).HasColumnName("line_total").HasPrecision(18, 4);
            entity.Property(x => x.PriceSource).HasColumnName("price_source").HasMaxLength(32);
            entity.Property(x => x.ResolvedUnitPrice).HasColumnName("resolved_unit_price").HasPrecision(18, 4);
            entity.Property(x => x.OverrideReason).HasColumnName("override_reason").HasMaxLength(512);
            entity.Property(x => x.OverrideByUserId).HasColumnName("override_by_user_id");
            MapAudit(entity);
            entity.HasOne(x => x.Invoice)
                .WithMany(x => x.Lines)
                .HasForeignKey(x => x.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Variant)
                .WithMany()
                .HasForeignKey(x => x.VariantId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
