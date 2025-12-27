using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace familyApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class DashboardSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubCategories_Category_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Category",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    MonthlyBudget = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    AnnualSavingsGoal = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppAccountLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Institution = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    AppAccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAccountLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAccountLines_AppAccounts_AppAccountId",
                        column: x => x.AppAccountId,
                        principalTable: "AppAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsNegative = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsTransfer = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    MovementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    SubCategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    AppAccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    AppAccountLineId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppMovements_SubCategories_SubCategoryId",
                        column: x => x.SubCategoryId,
                        principalTable: "SubCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AppMovements_AppAccountLines_AppAccountLineId",
                        column: x => x.AppAccountLineId,
                        principalTable: "AppAccountLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppMovements_AppAccounts_AppAccountId",
                        column: x => x.AppAccountId,
                        principalTable: "AppAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAccountLines_AppAccountId",
                table: "AppAccountLines",
                column: "AppAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAccounts_Username",
                table: "AppAccounts",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppMovements_AppAccountId_IsNegative",
                table: "AppMovements",
                columns: new[] { "AppAccountId", "IsNegative" });

            migrationBuilder.CreateIndex(
                name: "IX_AppMovements_AppAccountLineId",
                table: "AppMovements",
                column: "AppAccountLineId");

            migrationBuilder.CreateIndex(
                name: "IX_AppMovements_SubCategoryId",
                table: "AppMovements",
                column: "SubCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_AppMovements_MovementDate",
                table: "AppMovements",
                column: "MovementDate");

            migrationBuilder.CreateIndex(
                name: "IX_SubCategories_CategoryId",
                table: "SubCategories",
                column: "CategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppMovements");

            migrationBuilder.DropTable(
                name: "AppAccountLines");

            migrationBuilder.DropTable(
                name: "AppAccounts");

            migrationBuilder.DropTable(
                name: "SubCategories");
        }
    }
}
