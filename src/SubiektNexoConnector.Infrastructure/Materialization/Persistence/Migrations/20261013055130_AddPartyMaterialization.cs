using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartyMaterialization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Parties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Signature = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Type = table.Column<short>(type: "INTEGER", nullable: false),
                    Subtype = table.Column<byte>(type: "INTEGER", nullable: true),
                    TypeName = table.Column<string>(type: "TEXT", nullable: false),
                    SubtypeName = table.Column<string>(type: "TEXT", nullable: false),
                    CustomerStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomerStatusName = table.Column<string>(type: "TEXT", nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", nullable: true),
                    LastName = table.Column<string>(type: "TEXT", nullable: true),
                    CompanyName = table.Column<string>(type: "TEXT", nullable: true),
                    TaxId = table.Column<string>(type: "TEXT", nullable: true),
                    EuTaxId = table.Column<string>(type: "TEXT", nullable: true),
                    BusinessRegistryNumber = table.Column<string>(type: "TEXT", nullable: true),
                    NationalCourtRegisterNumber = table.Column<string>(type: "TEXT", nullable: true),
                    PartyGroup = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    FlagId = table.Column<int>(type: "INTEGER", nullable: true),
                    FlagComment = table.Column<string>(type: "TEXT", nullable: true),
                    TradeCreditEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    PaymentTermDays = table.Column<int>(type: "INTEGER", nullable: true),
                    MaximumPaymentDelayDays = table.Column<int>(type: "INTEGER", nullable: true),
                    MaximumUnpaidDocumentCount = table.Column<int>(type: "INTEGER", nullable: true),
                    OverallCreditLimit = table.Column<decimal>(type: "TEXT", nullable: true),
                    SalesCreditLimitEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SalesMaximumAmount = table.Column<decimal>(type: "TEXT", nullable: true),
                    SalesBelowMaximumAmountPercent = table.Column<decimal>(type: "TEXT", nullable: true),
                    SalesAboveMaximumAmountPercent = table.Column<decimal>(type: "TEXT", nullable: true),
                    GoodsIssueCreditLimitEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    GoodsIssueMaximumAmount = table.Column<decimal>(type: "TEXT", nullable: true),
                    GoodsIssueBelowMaximumAmountPercent = table.Column<decimal>(type: "TEXT", nullable: true),
                    GoodsIssueAboveMaximumAmountPercent = table.Column<decimal>(type: "TEXT", nullable: true),
                    OrderCreditLimitEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    OrderMaximumAmount = table.Column<decimal>(type: "TEXT", nullable: true),
                    OrderBelowMaximumAmountPercent = table.Column<decimal>(type: "TEXT", nullable: true),
                    OrderAboveMaximumAmountPercent = table.Column<decimal>(type: "TEXT", nullable: true),
                    RefreshedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartyAddresses",
                columns: table => new
                {
                    PartyId = table.Column<int>(type: "INTEGER", nullable: false),
                    AddressId = table.Column<int>(type: "INTEGER", nullable: false),
                    Street = table.Column<string>(type: "TEXT", nullable: true),
                    HouseNumber = table.Column<string>(type: "TEXT", nullable: true),
                    ApartmentNumber = table.Column<string>(type: "TEXT", nullable: true),
                    PostalCode = table.Column<string>(type: "TEXT", nullable: true),
                    City = table.Column<string>(type: "TEXT", nullable: true),
                    Municipality = table.Column<string>(type: "TEXT", nullable: true),
                    Voivodeship = table.Column<string>(type: "TEXT", nullable: true),
                    CountryId = table.Column<int>(type: "INTEGER", nullable: true),
                    AddressTypeName = table.Column<string>(type: "TEXT", nullable: true),
                    AddressTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    RefreshedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyAddresses", x => new { x.PartyId, x.AddressId });
                    table.ForeignKey(
                        name: "FK_PartyAddresses_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartyAdvancedFields",
                columns: table => new
                {
                    PartyId = table.Column<int>(type: "INTEGER", nullable: false),
                    FieldId = table.Column<string>(type: "TEXT", nullable: false),
                    DataType = table.Column<string>(type: "TEXT", nullable: false),
                    TextValue = table.Column<string>(type: "TEXT", nullable: true),
                    IntegerValue = table.Column<int>(type: "INTEGER", nullable: true),
                    DecimalValue = table.Column<decimal>(type: "TEXT", nullable: true),
                    BooleanValue = table.Column<bool>(type: "INTEGER", nullable: true),
                    DateValue = table.Column<DateTime>(type: "TEXT", nullable: true),
                    GuidValue = table.Column<Guid>(type: "TEXT", nullable: true),
                    DictionaryIntegerKey = table.Column<int>(type: "INTEGER", nullable: true),
                    DictionaryGuidKey = table.Column<Guid>(type: "TEXT", nullable: true),
                    RefreshedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyAdvancedFields", x => new { x.PartyId, x.FieldId });
                    table.ForeignKey(
                        name: "FK_PartyAdvancedFields_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartyBasicFields",
                columns: table => new
                {
                    PartyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Field1 = table.Column<string>(type: "TEXT", nullable: true),
                    Field2 = table.Column<string>(type: "TEXT", nullable: true),
                    Field3 = table.Column<string>(type: "TEXT", nullable: true),
                    Field4 = table.Column<string>(type: "TEXT", nullable: true),
                    Field5 = table.Column<string>(type: "TEXT", nullable: true),
                    Field6 = table.Column<string>(type: "TEXT", nullable: true),
                    Field7 = table.Column<string>(type: "TEXT", nullable: true),
                    Field8 = table.Column<string>(type: "TEXT", nullable: true),
                    PresentFieldsMask = table.Column<byte>(type: "INTEGER", nullable: false),
                    RefreshedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyBasicFields", x => x.PartyId);
                    table.ForeignKey(
                        name: "FK_PartyBasicFields_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartyContacts",
                columns: table => new
                {
                    PartyId = table.Column<int>(type: "INTEGER", nullable: false),
                    ContactId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false),
                    ContactTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    ContactType = table.Column<string>(type: "TEXT", nullable: true),
                    ContactValue = table.Column<string>(type: "TEXT", nullable: true),
                    ContactDescription = table.Column<string>(type: "TEXT", nullable: true),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    RefreshedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyContacts", x => new { x.PartyId, x.ContactId });
                    table.ForeignKey(
                        name: "FK_PartyContacts_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartyFeatures",
                columns: table => new
                {
                    PartyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    RefreshedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyFeatures", x => new { x.PartyId, x.Name });
                    table.ForeignKey(
                        name: "FK_PartyFeatures_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartyIndustries",
                columns: table => new
                {
                    PartyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    RefreshedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyIndustries", x => new { x.PartyId, x.Name });
                    table.ForeignKey(
                        name: "FK_PartyIndustries_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Parties_CustomerStatus_Type",
                table: "Parties",
                columns: new[] { "CustomerStatus", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Parties_RefreshedAtUtc",
                table: "Parties",
                column: "RefreshedAtUtc");

            migrationBuilder.CreateIndex(
                name: "UX_Parties_Signature",
                table: "Parties",
                column: "Signature",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartyAdvancedFields_FieldId",
                table: "PartyAdvancedFields",
                column: "FieldId");

            migrationBuilder.CreateIndex(
                name: "IX_PartyFeatures_Name",
                table: "PartyFeatures",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_PartyIndustries_Name",
                table: "PartyIndustries",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartyAddresses");

            migrationBuilder.DropTable(
                name: "PartyAdvancedFields");

            migrationBuilder.DropTable(
                name: "PartyBasicFields");

            migrationBuilder.DropTable(
                name: "PartyContacts");

            migrationBuilder.DropTable(
                name: "PartyFeatures");

            migrationBuilder.DropTable(
                name: "PartyIndustries");

            migrationBuilder.DropTable(
                name: "Parties");
        }
    }
}
