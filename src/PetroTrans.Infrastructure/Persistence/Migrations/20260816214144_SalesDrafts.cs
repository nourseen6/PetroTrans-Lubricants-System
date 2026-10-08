using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetroTrans.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SalesDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sales_invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    number = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    customer_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    invoice_date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    due_date = table.Column<DateTime>(type: "TEXT", nullable: true),
                    status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    goods_total = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    paid_total = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    remaining_total = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    payment_status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
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
                    table.PrimaryKey("PK_sales_invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_invoices_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_invoice_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    invoice_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    line_number = table.Column<int>(type: "INTEGER", nullable: false),
                    variant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    line_total = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    price_source = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    resolved_unit_price = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    override_reason = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    override_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    row_version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_invoice_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_invoice_lines_product_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_invoice_lines_sales_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "sales_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoice_lines_invoice_id",
                table: "sales_invoice_lines",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoice_lines_variant_id",
                table: "sales_invoice_lines",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoices_customer_id",
                table: "sales_invoices",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoices_number",
                table: "sales_invoices",
                column: "number",
                unique: true,
                filter: "number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoices_status_date",
                table: "sales_invoices",
                columns: new[] { "status", "invoice_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sales_invoice_lines");

            migrationBuilder.DropTable(
                name: "sales_invoices");
        }
    }
}
