using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetroTrans.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CoreOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "warehouse_id",
                table: "sales_invoices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "company_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    company_name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    logo_relative_path = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    default_locale = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    row_version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "party_ledger_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    party_kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    party_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    entry_type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    signed_amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    source_document_type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    source_document_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    notes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    row_version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_party_ledger_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payment_methods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    row_version = table.Column<int>(type: "INTEGER", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    deleted_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_methods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    code = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    phone = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    address = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    row_version = table.Column<int>(type: "INTEGER", nullable: false),
                    origin_installation_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    deleted_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "warehouses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    row_version = table.Column<int>(type: "INTEGER", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    deleted_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    party_kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    party_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    invoice_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    payment_method_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    paid_on = table.Column<DateTime>(type: "TEXT", nullable: false),
                    reference = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    notes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    row_version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payments_payment_methods_payment_method_id",
                        column: x => x.payment_method_id,
                        principalTable: "payment_methods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_sales_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "sales_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    number = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    supplier_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    invoice_date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    goods_total = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    notes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    row_version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_invoices_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "goods_receipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    number = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    supplier_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    document_date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    notes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    row_version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goods_receipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_goods_receipts_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_goods_receipts_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_adjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    direction = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    reason = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    notes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    occurred_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    row_version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_adjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_adjustments_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_balances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    variant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    on_hand = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_balances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_balances_product_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_balances_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_movements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    variant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    movement_type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    direction = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    source_document_type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    source_document_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    notes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    row_version = table.Column<int>(type: "INTEGER", nullable: false),
                    origin_installation_id = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_movements_product_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_movements_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_returns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    number = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    customer_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    original_sales_invoice_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    warehouse_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    return_date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    balance_posting_status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    notes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    row_version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_returns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_returns_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_returns_sales_invoices_original_sales_invoice_id",
                        column: x => x.original_sales_invoice_id,
                        principalTable: "sales_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_returns_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_transfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    from_warehouse_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    to_warehouse_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    notes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    occurred_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    row_version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_transfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_transfers_warehouses_from_warehouse_id",
                        column: x => x.from_warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_transfers_warehouses_to_warehouse_id",
                        column: x => x.to_warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_invoice_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    invoice_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    line_number = table.Column<int>(type: "INTEGER", nullable: false),
                    variant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    line_total = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    row_version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_invoice_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_invoice_lines_product_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_invoice_lines_purchase_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "purchase_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "goods_receipt_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    receipt_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    line_number = table.Column<int>(type: "INTEGER", nullable: false),
                    variant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    row_version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goods_receipt_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_goods_receipt_lines_goods_receipts_receipt_id",
                        column: x => x.receipt_id,
                        principalTable: "goods_receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_goods_receipt_lines_product_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_adjustment_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    adjustment_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    variant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    row_version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_adjustment_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_adjustment_lines_inventory_adjustments_adjustment_id",
                        column: x => x.adjustment_id,
                        principalTable: "inventory_adjustments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_adjustment_lines_product_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_return_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    return_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    line_number = table.Column<int>(type: "INTEGER", nullable: false),
                    variant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    row_version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_return_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_return_lines_product_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_return_lines_sales_returns_return_id",
                        column: x => x.return_id,
                        principalTable: "sales_returns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_transfer_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    transfer_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    variant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    row_version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_transfer_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_transfer_lines_product_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_transfer_lines_stock_transfers_transfer_id",
                        column: x => x.transfer_id,
                        principalTable: "stock_transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoices_warehouse_id",
                table: "sales_invoices",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_receipt_id",
                table: "goods_receipt_lines",
                column: "receipt_id");

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_variant_id",
                table: "goods_receipt_lines",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_supplier_id",
                table: "goods_receipts",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_warehouse_id",
                table: "goods_receipts",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_adjustment_lines_adjustment_id",
                table: "inventory_adjustment_lines",
                column: "adjustment_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_adjustment_lines_variant_id",
                table: "inventory_adjustment_lines",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_adjustments_warehouse_id",
                table: "inventory_adjustments",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_balances_variant_id",
                table: "inventory_balances",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_balances_wh_variant",
                table: "inventory_balances",
                columns: new[] { "warehouse_id", "variant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_variant_id",
                table: "inventory_movements",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_movements_wh_variant",
                table: "inventory_movements",
                columns: new[] { "warehouse_id", "variant_id" });

            migrationBuilder.CreateIndex(
                name: "ix_party_ledger_party",
                table: "party_ledger_entries",
                columns: new[] { "party_kind", "party_id" });

            migrationBuilder.CreateIndex(
                name: "IX_payments_invoice_id",
                table: "payments",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_payment_method_id",
                table: "payments",
                column: "payment_method_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoice_lines_invoice_id",
                table: "purchase_invoice_lines",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoice_lines_variant_id",
                table: "purchase_invoice_lines",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoices_supplier_id",
                table: "purchase_invoices",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_return_lines_return_id",
                table: "sales_return_lines",
                column: "return_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_return_lines_variant_id",
                table: "sales_return_lines",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_returns_customer_id",
                table: "sales_returns",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_returns_original_sales_invoice_id",
                table: "sales_returns",
                column: "original_sales_invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_returns_warehouse_id",
                table: "sales_returns",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfer_lines_transfer_id",
                table: "stock_transfer_lines",
                column: "transfer_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfer_lines_variant_id",
                table: "stock_transfer_lines",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_from_warehouse_id",
                table: "stock_transfers",
                column: "from_warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_to_warehouse_id",
                table: "stock_transfers",
                column: "to_warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_code",
                table: "suppliers",
                column: "code",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_sales_invoices_warehouses_warehouse_id",
                table: "sales_invoices",
                column: "warehouse_id",
                principalTable: "warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_sales_invoices_warehouses_warehouse_id",
                table: "sales_invoices");

            migrationBuilder.DropTable(
                name: "company_settings");

            migrationBuilder.DropTable(
                name: "goods_receipt_lines");

            migrationBuilder.DropTable(
                name: "inventory_adjustment_lines");

            migrationBuilder.DropTable(
                name: "inventory_balances");

            migrationBuilder.DropTable(
                name: "inventory_movements");

            migrationBuilder.DropTable(
                name: "party_ledger_entries");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "purchase_invoice_lines");

            migrationBuilder.DropTable(
                name: "sales_return_lines");

            migrationBuilder.DropTable(
                name: "stock_transfer_lines");

            migrationBuilder.DropTable(
                name: "goods_receipts");

            migrationBuilder.DropTable(
                name: "inventory_adjustments");

            migrationBuilder.DropTable(
                name: "payment_methods");

            migrationBuilder.DropTable(
                name: "purchase_invoices");

            migrationBuilder.DropTable(
                name: "sales_returns");

            migrationBuilder.DropTable(
                name: "stock_transfers");

            migrationBuilder.DropTable(
                name: "suppliers");

            migrationBuilder.DropTable(
                name: "warehouses");

            migrationBuilder.DropIndex(
                name: "IX_sales_invoices_warehouse_id",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "warehouse_id",
                table: "sales_invoices");
        }
    }
}
