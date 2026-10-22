using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Name);
                    table.CheckConstraint("CK_Category_Name", "length(Name) BETWEEN 1 AND 32 AND Name GLOB '[a-z]*' AND Name NOT GLOB '*[^a-z0-9_-]*'");
                });

            migrationBuilder.CreateTable(
                name: "ImportReceipts",
                columns: table => new
                {
                    BatchId = table.Column<string>(type: "TEXT", nullable: false),
                    Fingerprint = table.Column<string>(type: "TEXT", nullable: false),
                    Added = table.Column<int>(type: "INTEGER", nullable: false),
                    Duplicates = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportReceipts", x => x.BatchId);
                });

            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ExternalId = table.Column<string>(type: "TEXT", nullable: false),
                    Day = table.Column<int>(type: "INTEGER", nullable: false),
                    CategoryName = table.Column<string>(type: "TEXT", nullable: false),
                    AmountMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.Sequence);
                    table.CheckConstraint("CK_Expense_Amount", "typeof(AmountMinor) = 'integer' AND AmountMinor BETWEEN 1 AND 99999999");
                    table.CheckConstraint("CK_Expense_Day", "typeof(Day) = 'integer' AND Day BETWEEN 0 AND 3652058");
                    table.CheckConstraint("CK_Expense_Id", "length(ExternalId) = 36");
                    table.CheckConstraint("CK_Expense_Version", "typeof(Version) = 'integer' AND Version >= 0");
                    table.ForeignKey(
                        name: "FK_Expenses_Categories_CategoryName",
                        column: x => x.CategoryName,
                        principalTable: "Categories",
                        principalColumn: "Name",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_CategoryName",
                table: "Expenses",
                column: "CategoryName");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_Day_Sequence",
                table: "Expenses",
                columns: new[] { "Day", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ExternalId",
                table: "Expenses",
                column: "ExternalId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Expenses");

            migrationBuilder.DropTable(
                name: "ImportReceipts");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
