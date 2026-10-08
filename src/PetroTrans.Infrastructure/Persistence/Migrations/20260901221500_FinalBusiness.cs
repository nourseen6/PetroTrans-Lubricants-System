using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetroTrans.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinalBusiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer_variant_price_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    customer_variant_price_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    customer_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    variant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    old_unit_price = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    new_unit_price = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    change_kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    reason = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    changed_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    changed_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_variant_price_history", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_customer_variant_price_history_changed_at",
                table: "customer_variant_price_history",
                column: "changed_at");

            migrationBuilder.CreateIndex(
                name: "ix_customer_variant_price_history_customer",
                table: "customer_variant_price_history",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_variant_price_history_variant",
                table: "customer_variant_price_history",
                column: "variant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_variant_price_history");
        }
    }
}
