using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductMaterializationDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FlagComment",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FlagId",
                table: "Products",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefreshedAtUtc",
                table: "Products",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "SupplierLeadTimeDays",
                table: "Products",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TypeName",
                table: "Products",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TypeSymbol",
                table: "Products",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ProductAdvancedFields",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    FieldId = table.Column<string>(type: "TEXT", nullable: false),
                    DataType = table.Column<string>(type: "TEXT", nullable: false),
                    TextValue = table.Column<string>(type: "TEXT", nullable: true),
                    IntegerValue = table.Column<int>(type: "INTEGER", nullable: true),
                    DecimalValue = table.Column<decimal>(type: "TEXT", nullable: true),
                    BooleanValue = table.Column<bool>(type: "INTEGER", nullable: true),
                    DateValue = table.Column<DateTime>(type: "TEXT", nullable: true),
                    GuidValue = table.Column<Guid>(type: "TEXT", nullable: true),
                    RefreshedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductAdvancedFields", x => new { x.ProductId, x.FieldId });
                    table.ForeignKey(
                        name: "FK_ProductAdvancedFields_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductBasicFields",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    Field1 = table.Column<string>(type: "TEXT", nullable: true),
                    Field2 = table.Column<string>(type: "TEXT", nullable: true),
                    Field3 = table.Column<string>(type: "TEXT", nullable: true),
                    Field4 = table.Column<string>(type: "TEXT", nullable: true),
                    Field5 = table.Column<string>(type: "TEXT", nullable: true),
                    Field6 = table.Column<string>(type: "TEXT", nullable: true),
                    Field7 = table.Column<string>(type: "TEXT", nullable: true),
                    Field8 = table.Column<string>(type: "TEXT", nullable: true),
                    RefreshedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductBasicFields", x => x.ProductId);
                    table.ForeignKey(
                        name: "FK_ProductBasicFields_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductPrices",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    PriceLevelName = table.Column<string>(type: "TEXT", nullable: false),
                    PriceNet = table.Column<decimal>(type: "TEXT", nullable: false),
                    PriceGross = table.Column<decimal>(type: "TEXT", nullable: false),
                    RefreshedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPrices", x => new { x.ProductId, x.PriceLevelName });
                    table.ForeignKey(
                        name: "FK_ProductPrices_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductStocks",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    WarehouseSymbol = table.Column<string>(type: "TEXT", nullable: false),
                    AvailableStock = table.Column<decimal>(type: "TEXT", nullable: false),
                    ReservedStock = table.Column<decimal>(type: "TEXT", nullable: false),
                    RefreshedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductStocks", x => new { x.ProductId, x.WarehouseSymbol });
                    table.ForeignKey(
                        name: "FK_ProductStocks_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductSuppliers",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Nip = table.Column<string>(type: "TEXT", nullable: true),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false),
                    SupplierProductSymbol = table.Column<string>(type: "TEXT", nullable: true),
                    SupplierProductName = table.Column<string>(type: "TEXT", nullable: true),
                    RefreshedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductSuppliers", x => new { x.ProductId, x.SupplierId });
                    table.ForeignKey(
                        name: "FK_ProductSuppliers_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductAdvancedFields_FieldId",
                table: "ProductAdvancedFields",
                column: "FieldId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSuppliers_SupplierId",
                table: "ProductSuppliers",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductAdvancedFields");

            migrationBuilder.DropTable(
                name: "ProductBasicFields");

            migrationBuilder.DropTable(
                name: "ProductPrices");

            migrationBuilder.DropTable(
                name: "ProductStocks");

            migrationBuilder.DropTable(
                name: "ProductSuppliers");

            migrationBuilder.DropColumn(
                name: "FlagComment",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "FlagId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "RefreshedAtUtc",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SupplierLeadTimeDays",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TypeName",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TypeSymbol",
                table: "Products");
        }
    }
}
