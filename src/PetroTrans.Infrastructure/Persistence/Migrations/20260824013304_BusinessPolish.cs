using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetroTrans.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BusinessPolish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "discount_amount",
                table: "sales_invoices",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "has_manual_total",
                table: "sales_invoices",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "lines_subtotal",
                table: "sales_invoices",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "manual_adjustment",
                table: "sales_invoices",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "manual_total_reason",
                table: "sales_invoices",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE sales_invoices
                SET lines_subtotal = goods_total,
                    discount_amount = 0,
                    manual_adjustment = 0,
                    has_manual_total = 0
                WHERE lines_subtotal = 0 OR lines_subtotal IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "invoice_id",
                table: "payments",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.CreateTable(
                name: "assistant_drafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    intent_type = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    user_text = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    draft_json = table.Column<string>(type: "TEXT", nullable: false),
                    result_json = table.Column<string>(type: "TEXT", nullable: true),
                    error = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    row_version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assistant_drafts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "customer_variant_prices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    customer_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    variant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    unit_price = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    row_version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_variant_prices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_variant_prices_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_customer_variant_prices_product_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payments_party_date",
                table: "payments",
                columns: new[] { "party_kind", "party_id", "paid_on" });

            migrationBuilder.CreateIndex(
                name: "ix_assistant_drafts_status_created",
                table: "assistant_drafts",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_variant_prices_customer_variant",
                table: "customer_variant_prices",
                columns: new[] { "customer_id", "variant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_variant_prices_variant_id",
                table: "customer_variant_prices",
                column: "variant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assistant_drafts");

            migrationBuilder.DropTable(
                name: "customer_variant_prices");

            migrationBuilder.DropIndex(
                name: "ix_payments_party_date",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "discount_amount",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "has_manual_total",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "lines_subtotal",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "manual_adjustment",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "manual_total_reason",
                table: "sales_invoices");

            migrationBuilder.AlterColumn<Guid>(
                name: "invoice_id",
                table: "payments",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
