using Microsoft.EntityFrameworkCore;
using PetroTrans.Domain.Assistant;
using PetroTrans.Domain.Finance;
using PetroTrans.Domain.Inventory;
using PetroTrans.Domain.Parties;
using PetroTrans.Domain.Purchasing;
using PetroTrans.Domain.Returns;
using PetroTrans.Domain.Settings;

namespace PetroTrans.Infrastructure.Persistence;

public sealed partial class AppDbContext
{
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<InventoryBalance> InventoryBalances => Set<InventoryBalance>();
    public DbSet<InventoryAdjustment> InventoryAdjustments => Set<InventoryAdjustment>();
    public DbSet<InventoryAdjustmentLine> InventoryAdjustmentLines => Set<InventoryAdjustmentLine>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<StockTransferLine> StockTransferLines => Set<StockTransferLine>();
    public DbSet<PartyLedgerEntry> PartyLedgerEntries => Set<PartyLedgerEntry>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptLine> GoodsReceiptLines => Set<GoodsReceiptLine>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseInvoiceLine> PurchaseInvoiceLines => Set<PurchaseInvoiceLine>();
    public DbSet<SalesReturn> SalesReturns => Set<SalesReturn>();
    public DbSet<SalesReturnLine> SalesReturnLines => Set<SalesReturnLine>();
    public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();
    public DbSet<AssistantDraft> AssistantDrafts => Set<AssistantDraft>();
    public DbSet<TreasuryEntry> TreasuryEntries => Set<TreasuryEntry>();

    private void ConfigureOperations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.ToTable("warehouses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
            entity.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            MapAudit(entity);
            entity.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            entity.Property(x => x.DeletedByUserId).HasColumnName("deleted_by_user_id");
            entity.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<InventoryMovement>(entity =>
        {
            entity.ToTable("inventory_movements");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.VariantId).HasColumnName("variant_id").IsRequired();
            entity.Property(x => x.WarehouseId).HasColumnName("warehouse_id").IsRequired();
            entity.Property(x => x.MovementType).HasColumnName("movement_type").HasMaxLength(32).IsRequired();
            entity.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(18, 4).IsRequired();
            entity.Property(x => x.Direction).HasColumnName("direction").HasMaxLength(8).IsRequired();
            entity.Property(x => x.SourceDocumentType).HasColumnName("source_document_type").HasMaxLength(32).IsRequired();
            entity.Property(x => x.SourceDocumentId).HasColumnName("source_document_id").IsRequired();
            entity.Property(x => x.OccurredAt).HasColumnName("occurred_at").IsRequired();
            entity.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(1024);
            entity.Property(x => x.OriginInstallationId).HasColumnName("origin_installation_id");
            MapAudit(entity);
            entity.HasIndex(x => new { x.WarehouseId, x.VariantId }).HasDatabaseName("ix_inventory_movements_wh_variant");
            entity.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InventoryBalance>(entity =>
        {
            entity.ToTable("inventory_balances");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.WarehouseId).HasColumnName("warehouse_id").IsRequired();
            entity.Property(x => x.VariantId).HasColumnName("variant_id").IsRequired();
            entity.Property(x => x.OnHand).HasColumnName("on_hand").HasPrecision(18, 4).IsRequired();
            entity.HasIndex(x => new { x.WarehouseId, x.VariantId }).IsUnique().HasDatabaseName("ix_inventory_balances_wh_variant");
            entity.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InventoryAdjustment>(entity =>
        {
            entity.ToTable("inventory_adjustments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.WarehouseId).HasColumnName("warehouse_id").IsRequired();
            entity.Property(x => x.Direction).HasColumnName("direction").HasMaxLength(8).IsRequired();
            entity.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(512).IsRequired();
            entity.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(1024);
            entity.Property(x => x.OccurredAt).HasColumnName("occurred_at").IsRequired();
            MapAudit(entity);
            entity.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InventoryAdjustmentLine>(entity =>
        {
            entity.ToTable("inventory_adjustment_lines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AdjustmentId).HasColumnName("adjustment_id").IsRequired();
            entity.Property(x => x.VariantId).HasColumnName("variant_id").IsRequired();
            entity.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(18, 4).IsRequired();
            MapAudit(entity);
            entity.HasOne(x => x.Adjustment).WithMany(x => x.Lines).HasForeignKey(x => x.AdjustmentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockTransfer>(entity =>
        {
            entity.ToTable("stock_transfers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FromWarehouseId).HasColumnName("from_warehouse_id").IsRequired();
            entity.Property(x => x.ToWarehouseId).HasColumnName("to_warehouse_id").IsRequired();
            entity.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(1024);
            entity.Property(x => x.OccurredAt).HasColumnName("occurred_at").IsRequired();
            MapAudit(entity);
            entity.HasOne(x => x.FromWarehouse).WithMany().HasForeignKey(x => x.FromWarehouseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ToWarehouse).WithMany().HasForeignKey(x => x.ToWarehouseId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockTransferLine>(entity =>
        {
            entity.ToTable("stock_transfer_lines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TransferId).HasColumnName("transfer_id").IsRequired();
            entity.Property(x => x.VariantId).HasColumnName("variant_id").IsRequired();
            entity.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(18, 4).IsRequired();
            MapAudit(entity);
            entity.HasOne(x => x.Transfer).WithMany(x => x.Lines).HasForeignKey(x => x.TransferId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PartyLedgerEntry>(entity =>
        {
            entity.ToTable("party_ledger_entries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PartyKind).HasColumnName("party_kind").HasMaxLength(16).IsRequired();
            entity.Property(x => x.PartyId).HasColumnName("party_id").IsRequired();
            entity.Property(x => x.EntryType).HasColumnName("entry_type").HasMaxLength(32).IsRequired();
            entity.Property(x => x.SignedAmount).HasColumnName("signed_amount").HasPrecision(18, 4).IsRequired();
            entity.Property(x => x.SourceDocumentType).HasColumnName("source_document_type").HasMaxLength(32).IsRequired();
            entity.Property(x => x.SourceDocumentId).HasColumnName("source_document_id").IsRequired();
            entity.Property(x => x.OccurredAt).HasColumnName("occurred_at").IsRequired();
            entity.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(1024);
            MapAudit(entity);
            entity.HasIndex(x => new { x.PartyKind, x.PartyId }).HasDatabaseName("ix_party_ledger_party");
        });

        modelBuilder.Entity<PaymentMethod>(entity =>
        {
            entity.ToTable("payment_methods");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
            entity.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            MapAudit(entity);
            entity.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            entity.Property(x => x.DeletedByUserId).HasColumnName("deleted_by_user_id");
            entity.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("payments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PartyKind).HasColumnName("party_kind").HasMaxLength(16).IsRequired();
            entity.Property(x => x.PartyId).HasColumnName("party_id").IsRequired();
            entity.Property(x => x.InvoiceId).HasColumnName("invoice_id");
            entity.Property(x => x.PaymentMethodId).HasColumnName("payment_method_id").IsRequired();
            entity.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 4).IsRequired();
            entity.Property(x => x.PaidOn).HasColumnName("paid_on").IsRequired();
            entity.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(128);
            entity.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(1024);
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            MapAudit(entity);
            entity.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
            entity.HasOne(x => x.PaymentMethod).WithMany().HasForeignKey(x => x.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Allocations).WithOne(x => x.Payment).HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.PartyKind, x.PartyId, x.PaidOn }).HasDatabaseName("ix_payments_party_date");
        });

        modelBuilder.Entity<PaymentAllocation>(entity =>
        {
            entity.ToTable("payment_allocations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PaymentId).HasColumnName("payment_id").IsRequired();
            entity.Property(x => x.InvoiceId).HasColumnName("invoice_id").IsRequired();
            entity.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 4).IsRequired();
            MapAudit(entity);
            entity.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.PaymentId).HasDatabaseName("ix_payment_allocations_payment");
            entity.HasIndex(x => x.InvoiceId).HasDatabaseName("ix_payment_allocations_invoice");
        });

        modelBuilder.Entity<SupplierPayment>(entity =>
        {
            entity.ToTable("supplier_payments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SupplierId).HasColumnName("supplier_id").IsRequired();
            entity.Property(x => x.PaymentMethodId).HasColumnName("payment_method_id").IsRequired();
            entity.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 4).IsRequired();
            entity.Property(x => x.PaidOn).HasColumnName("paid_on").IsRequired();
            entity.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(128);
            entity.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(1024);
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            MapAudit(entity);
            entity.HasIndex(x => x.SupplierId).HasDatabaseName("ix_supplier_payments_supplier");
            entity.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PaymentMethod).WithMany().HasForeignKey(x => x.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.ToTable("suppliers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(32).IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(256).IsRequired();
            entity.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(64);
            entity.Property(x => x.Address).HasColumnName("address").HasMaxLength(512);
            entity.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            entity.Property(x => x.OriginInstallationId).HasColumnName("origin_installation_id");
            MapAudit(entity);
            entity.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            entity.Property(x => x.DeletedByUserId).HasColumnName("deleted_by_user_id");
            entity.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ix_suppliers_code").HasFilter("deleted_at IS NULL");
            entity.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<GoodsReceipt>(entity =>
        {
            entity.ToTable("goods_receipts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Number).HasColumnName("number").HasMaxLength(32);
            entity.Property(x => x.SupplierId).HasColumnName("supplier_id").IsRequired();
            entity.Property(x => x.WarehouseId).HasColumnName("warehouse_id").IsRequired();
            entity.Property(x => x.DocumentDate).HasColumnName("document_date").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(1024);
            MapAudit(entity);
            entity.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GoodsReceiptLine>(entity =>
        {
            entity.ToTable("goods_receipt_lines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ReceiptId).HasColumnName("receipt_id").IsRequired();
            entity.Property(x => x.LineNumber).HasColumnName("line_number").IsRequired();
            entity.Property(x => x.VariantId).HasColumnName("variant_id").IsRequired();
            entity.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(18, 4).IsRequired();
            MapAudit(entity);
            entity.HasOne(x => x.Receipt).WithMany(x => x.Lines).HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PurchaseInvoice>(entity =>
        {
            entity.ToTable("purchase_invoices");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Number).HasColumnName("number").HasMaxLength(32);
            entity.Property(x => x.SupplierId).HasColumnName("supplier_id").IsRequired();
            entity.Property(x => x.InvoiceDate).HasColumnName("invoice_date").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.GoodsTotal).HasColumnName("goods_total").HasPrecision(18, 4).IsRequired();
            entity.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(1024);
            MapAudit(entity);
            entity.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PurchaseInvoiceLine>(entity =>
        {
            entity.ToTable("purchase_invoice_lines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.InvoiceId).HasColumnName("invoice_id").IsRequired();
            entity.Property(x => x.LineNumber).HasColumnName("line_number").IsRequired();
            entity.Property(x => x.VariantId).HasColumnName("variant_id").IsRequired();
            entity.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(18, 4).IsRequired();
            entity.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 4).IsRequired();
            entity.Property(x => x.LineTotal).HasColumnName("line_total").HasPrecision(18, 4).IsRequired();
            MapAudit(entity);
            entity.HasOne(x => x.Invoice).WithMany(x => x.Lines).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SalesReturn>(entity =>
        {
            entity.ToTable("sales_returns");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Number).HasColumnName("number").HasMaxLength(32);
            entity.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
            entity.Property(x => x.OriginalSalesInvoiceId).HasColumnName("original_sales_invoice_id");
            entity.Property(x => x.WarehouseId).HasColumnName("warehouse_id").IsRequired();
            entity.Property(x => x.ReturnDate).HasColumnName("return_date").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.BalancePostingStatus).HasColumnName("balance_posting_status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(1024);
            MapAudit(entity);
            entity.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.OriginalSalesInvoice).WithMany().HasForeignKey(x => x.OriginalSalesInvoiceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SalesReturnLine>(entity =>
        {
            entity.ToTable("sales_return_lines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ReturnId).HasColumnName("return_id").IsRequired();
            entity.Property(x => x.LineNumber).HasColumnName("line_number").IsRequired();
            entity.Property(x => x.VariantId).HasColumnName("variant_id").IsRequired();
            entity.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(18, 4).IsRequired();
            MapAudit(entity);
            entity.HasOne(x => x.Return).WithMany(x => x.Lines).HasForeignKey(x => x.ReturnId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CompanySettings>(entity =>
        {
            entity.ToTable("company_settings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CompanyName).HasColumnName("company_name").HasMaxLength(256).IsRequired();
            entity.Property(x => x.LogoRelativePath).HasColumnName("logo_relative_path").HasMaxLength(512);
            entity.Property(x => x.DefaultLocale).HasColumnName("default_locale").HasMaxLength(8).IsRequired();
            MapAudit(entity);
        });

        modelBuilder.Entity<AssistantDraft>(entity =>
        {
            entity.ToTable("assistant_drafts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.IntentType).HasColumnName("intent_type").HasMaxLength(64).IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.UserText).HasColumnName("user_text").HasMaxLength(4000).IsRequired();
            entity.Property(x => x.DraftJson).HasColumnName("draft_json").IsRequired();
            entity.Property(x => x.ResultJson).HasColumnName("result_json");
            entity.Property(x => x.Error).HasColumnName("error").HasMaxLength(1024);
            MapAudit(entity);
            entity.HasIndex(x => new { x.Status, x.CreatedAt }).HasDatabaseName("ix_assistant_drafts_status_created");
        });

        modelBuilder.Entity<TreasuryEntry>(entity =>
        {
            entity.ToTable("treasury_entries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OccurredOn).HasColumnName("occurred_on").IsRequired();
            entity.Property(x => x.Direction).HasColumnName("direction").HasMaxLength(8).IsRequired();
            entity.Property(x => x.Category).HasColumnName("category").HasMaxLength(32);
            entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(256).IsRequired();
            entity.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 4).IsRequired();
            entity.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(1024);
            entity.Property(x => x.SourceDocumentType).HasColumnName("source_document_type").HasMaxLength(32);
            entity.Property(x => x.SourceDocumentId).HasColumnName("source_document_id");
            MapAudit(entity);
            entity.HasIndex(x => x.OccurredOn).HasDatabaseName("ix_treasury_entries_occurred_on");
            entity.HasIndex(x => new { x.SourceDocumentType, x.SourceDocumentId })
                .HasDatabaseName("ix_treasury_entries_source");
        });
    }
}
