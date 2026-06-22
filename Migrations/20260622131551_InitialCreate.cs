using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MapaMensal.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Month = table.Column<int>(type: "INTEGER", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Holidays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsNational = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holidays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Client = table.Column<string>(type: "TEXT", nullable: true),
                    DailyRate = table.Column<decimal>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkDays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Mark = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkDays_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AppConfigs",
                columns: new[] { "Id", "Key", "Value" },
                values: new object[] { 1, "IvaRate", "0.23" });

            migrationBuilder.InsertData(
                table: "Holidays",
                columns: new[] { "Id", "Date", "IsNational", "Name" },
                values: new object[,]
                {
                    { 1, new DateOnly(2026, 6, 4), true, "Corpo de Deus" },
                    { 2, new DateOnly(2026, 6, 10), true, "Dia de Portugal" },
                    { 3, new DateOnly(2026, 8, 15), true, "Assunção de Nossa Senhora" },
                    { 4, new DateOnly(2026, 10, 5), true, "Implantação da República" },
                    { 5, new DateOnly(2026, 11, 1), true, "Todos os Santos" },
                    { 6, new DateOnly(2026, 12, 1), true, "Restauração da Independência" },
                    { 7, new DateOnly(2026, 12, 8), true, "Imaculada Conceição" },
                    { 8, new DateOnly(2026, 12, 25), true, "Natal" }
                });

            migrationBuilder.InsertData(
                table: "Projects",
                columns: new[] { "Id", "Client", "DailyRate", "IsActive", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 1, null, 220m, true, "KCSIT/FNAC", 1 },
                    { 2, null, 242m, true, "CLOSER/NB", 2 },
                    { 3, null, 204m, true, "Capgemini/DPD", 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppConfigs_Key",
                table: "AppConfigs",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_Date",
                table: "Holidays",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_WorkDays_ProjectId_Date",
                table: "WorkDays",
                columns: new[] { "ProjectId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppConfigs");

            migrationBuilder.DropTable(
                name: "Expenses");

            migrationBuilder.DropTable(
                name: "Holidays");

            migrationBuilder.DropTable(
                name: "WorkDays");

            migrationBuilder.DropTable(
                name: "Projects");
        }
    }
}
