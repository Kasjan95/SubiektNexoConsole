using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterializationPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaterializationPolicies",
                columns: table => new
                {
                    PolicyKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LifetimeSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterializationPolicies", x => x.PolicyKey);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaterializationPolicies");
        }
    }
}
