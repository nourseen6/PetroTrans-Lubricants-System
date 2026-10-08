using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PetroTrans.Infrastructure.Persistence;

#nullable disable

namespace PetroTrans.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260903191500_PriceLayersAndTreasuryLinks")]
    public partial class PriceLayersAndTreasuryLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "standard_purchase_price",
                table: "product_variants",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_document_type",
                table: "treasury_entries",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_document_id",
                table: "treasury_entries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_treasury_entries_source",
                table: "treasury_entries",
                columns: new[] { "source_document_type", "source_document_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_treasury_entries_source",
                table: "treasury_entries");

            migrationBuilder.DropColumn(
                name: "standard_purchase_price",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "source_document_type",
                table: "treasury_entries");

            migrationBuilder.DropColumn(
                name: "source_document_id",
                table: "treasury_entries");
        }
    }
}
