using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvancedFieldDefinitionsMaterialization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdvancedFieldGroups",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Target = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdvancedFieldGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdvancedFieldDefinitions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Target = table.Column<string>(type: "TEXT", nullable: false),
                    FieldId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    DataType = table.Column<string>(type: "TEXT", nullable: false),
                    Required = table.Column<bool>(type: "INTEGER", nullable: false),
                    Visible = table.Column<bool>(type: "INTEGER", nullable: false),
                    Editable = table.Column<bool>(type: "INTEGER", nullable: false),
                    Cloneable = table.Column<bool>(type: "INTEGER", nullable: false),
                    Precision = table.Column<int>(type: "INTEGER", nullable: true),
                    MinVisibleLines = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxVisibleLines = table.Column<int>(type: "INTEGER", nullable: true),
                    DefaultValueJson = table.Column<string>(type: "TEXT", nullable: true),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    DictionaryKind = table.Column<string>(type: "TEXT", nullable: true),
                    DictionaryKeyType = table.Column<string>(type: "TEXT", nullable: true),
                    SystemDictionary = table.Column<string>(type: "TEXT", nullable: true),
                    DictionaryOptionsAvailable = table.Column<bool>(type: "INTEGER", nullable: false),
                    GroupId = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdvancedFieldDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdvancedFieldDefinitions_AdvancedFieldGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "AdvancedFieldGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AdvancedFieldDictionaryOptions",
                columns: table => new
                {
                    DefinitionId = table.Column<long>(type: "INTEGER", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: true),
                    Position = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdvancedFieldDictionaryOptions", x => new { x.DefinitionId, x.Key });
                    table.ForeignKey(
                        name: "FK_AdvancedFieldDictionaryOptions_AdvancedFieldDefinitions_DefinitionId",
                        column: x => x.DefinitionId,
                        principalTable: "AdvancedFieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdvancedFieldDefinitions_GroupId",
                table: "AdvancedFieldDefinitions",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_AdvancedFieldDefinitions_Target_FieldId",
                table: "AdvancedFieldDefinitions",
                columns: new[] { "Target", "FieldId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdvancedFieldGroups_Target_Name_Position",
                table: "AdvancedFieldGroups",
                columns: new[] { "Target", "Name", "Position" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdvancedFieldDictionaryOptions");

            migrationBuilder.DropTable(
                name: "AdvancedFieldDefinitions");

            migrationBuilder.DropTable(
                name: "AdvancedFieldGroups");
        }
    }
}
