using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteProductMaterialization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "PresentFieldsMask",
                table: "ProductBasicFields",
                type: "INTEGER",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<Guid>(
                name: "DictionaryGuidKey",
                table: "ProductAdvancedFields",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DictionaryIntegerKey",
                table: "ProductAdvancedFields",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PresentFieldsMask",
                table: "ProductBasicFields");

            migrationBuilder.DropColumn(
                name: "DictionaryGuidKey",
                table: "ProductAdvancedFields");

            migrationBuilder.DropColumn(
                name: "DictionaryIntegerKey",
                table: "ProductAdvancedFields");
        }
    }
}
