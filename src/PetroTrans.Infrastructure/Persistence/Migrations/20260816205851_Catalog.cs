using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetroTrans.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Catalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer_types",
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
                    table.PrimaryKey("PK_customer_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "number_series",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    document_type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    prefix = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    padding = table.Column<int>(type: "INTEGER", nullable: false),
                    next_value = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    row_version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_number_series", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    brand = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    category = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    specification = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
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
                    table.PrimaryKey("PK_products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    customer_type_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    contact_person = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    phone = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    whatsapp = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    address = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    credit_limit = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    payment_term_days = table.Column<int>(type: "INTEGER", nullable: true),
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
                    table.PrimaryKey("PK_customers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customers_customer_types_customer_type_id",
                        column: x => x.customer_type_id,
                        principalTable: "customer_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_variants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    product_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    packaging_type = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    packaging_size = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    sku = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    barcode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    min_stock = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    standard_wholesale_price = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
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
                    table.PrimaryKey("PK_product_variants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_variants_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_media",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    product_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    variant_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    relative_path = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    row_version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_media", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_media_product_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_media_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_customers_code",
                table: "customers",
                column: "code",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_customers_customer_type_id",
                table: "customers",
                column: "customer_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_number_series_document_type",
                table: "number_series",
                column: "document_type",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_media_product_id",
                table: "product_media",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_media_variant_id",
                table: "product_media",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_product_id",
                table: "product_variants",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_variants_sku",
                table: "product_variants",
                column: "sku",
                unique: true,
                filter: "sku IS NOT NULL AND deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "number_series");

            migrationBuilder.DropTable(
                name: "product_media");

            migrationBuilder.DropTable(
                name: "customer_types");

            migrationBuilder.DropTable(
                name: "product_variants");

            migrationBuilder.DropTable(
                name: "products");
        }
    }
}
