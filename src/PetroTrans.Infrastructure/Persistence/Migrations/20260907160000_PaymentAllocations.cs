using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PetroTrans.Infrastructure.Persistence;

#nullable disable

namespace PetroTrans.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260907160000_PaymentAllocations")]
    public partial class PaymentAllocations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payment_allocations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    payment_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    invoice_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    row_version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_allocations", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_allocations_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_payment_allocations_sales_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "sales_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payment_allocations_payment",
                table: "payment_allocations",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_allocations_invoice",
                table: "payment_allocations",
                column: "invoice_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "payment_allocations");
        }
    }
}
