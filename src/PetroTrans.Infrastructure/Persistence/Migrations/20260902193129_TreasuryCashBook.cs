using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetroTrans.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TreasuryCashBook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "treasury_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    occurred_on = table.Column<DateTime>(type: "TEXT", nullable: false),
                    direction = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    category = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    description = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    notes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    row_version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_treasury_entries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_treasury_entries_occurred_on",
                table: "treasury_entries",
                column: "occurred_on");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "treasury_entries");
        }
    }
}
