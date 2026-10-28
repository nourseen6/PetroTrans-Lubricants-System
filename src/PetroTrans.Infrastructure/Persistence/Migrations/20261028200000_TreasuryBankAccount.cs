using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PetroTrans.Infrastructure.Persistence;

#nullable disable

namespace PetroTrans.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20261028200000_TreasuryBankAccount")]
    public partial class TreasuryBankAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every existing row stays in the cash book, so old balances do not move.
            migrationBuilder.AddColumn<string>(
                name: "account",
                table: "treasury_entries",
                type: "TEXT",
                maxLength: 8,
                nullable: false,
                defaultValue: "cash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "account",
                table: "treasury_entries");
        }
    }
}
