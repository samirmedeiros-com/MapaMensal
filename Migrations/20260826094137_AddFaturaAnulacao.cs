using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MapaMensal.Migrations
{
    /// <inheritdoc />
    public partial class AddFaturaAnulacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_mapa_timesheet_faturas_ProjectId_Year_Month",
                table: "mapa_timesheet_faturas");

            migrationBuilder.AddColumn<DateTime>(
                name: "AnuladaEm",
                table: "mapa_timesheet_faturas",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JustificativaAnulacao",
                table: "mapa_timesheet_faturas",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_mapa_timesheet_faturas_ProjectId_Year_Month",
                table: "mapa_timesheet_faturas",
                columns: new[] { "ProjectId", "Year", "Month" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_mapa_timesheet_faturas_ProjectId_Year_Month",
                table: "mapa_timesheet_faturas");

            migrationBuilder.DropColumn(
                name: "AnuladaEm",
                table: "mapa_timesheet_faturas");

            migrationBuilder.DropColumn(
                name: "JustificativaAnulacao",
                table: "mapa_timesheet_faturas");

            migrationBuilder.CreateIndex(
                name: "IX_mapa_timesheet_faturas_ProjectId_Year_Month",
                table: "mapa_timesheet_faturas",
                columns: new[] { "ProjectId", "Year", "Month" },
                unique: true);
        }
    }
}
