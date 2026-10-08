using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetroTrans.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupplierPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "supplier_payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    supplier_id = table.Column<Guid>(type: "TEXT", nullable: false),
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
                    table.PrimaryKey("PK_supplier_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supplier_payments_payment_methods_payment_method_id",
                        column: x => x.payment_method_id,
                        principalTable: "payment_methods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_payments_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_payments_payment_method_id",
                table: "supplier_payments",
                column: "payment_method_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_payments_supplier",
                table: "supplier_payments",
                column: "supplier_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "supplier_payments");
        }
    }
}
