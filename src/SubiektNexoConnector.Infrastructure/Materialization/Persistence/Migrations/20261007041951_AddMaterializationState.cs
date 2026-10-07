using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterializationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaterializationStates",
                columns: table => new
                {
                    PolicyKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NextCursor = table.Column<int>(type: "INTEGER", nullable: true),
                    CycleStartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastBatchCompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastCycleCompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastErrorAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterializationStates", x => x.PolicyKey);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaterializationStates");
        }
    }
}
