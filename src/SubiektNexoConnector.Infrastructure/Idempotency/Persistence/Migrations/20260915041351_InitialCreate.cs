using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SubiektNexoConnector.Infrastructure.Idempotency.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IdempotencyOperations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Method = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    RequestTarget = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    RequestHash = table.Column<string>(type: "TEXT", fixedLength: true, maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "INTEGER", nullable: true),
                    ResponseContentType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ResponseLocation = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    ResponseBody = table.Column<byte[]>(type: "BLOB", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyOperations", x => x.Id);
                    table.CheckConstraint("CK_IdempotencyOperations_State", "State IN ('Pending', 'Executing', 'Completed', 'Indeterminate')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyOperations_ExpiresAtUtc",
                table: "IdempotencyOperations",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "UX_IdempotencyOperations_ClientId_Key",
                table: "IdempotencyOperations",
                columns: new[] { "ClientId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotencyOperations");
        }
    }
}
