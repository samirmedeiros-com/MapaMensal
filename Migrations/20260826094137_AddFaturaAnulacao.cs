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
            // O MySQL não deixa apagar o índice único diretamente: ele suporta a
            // foreign key de ProjectId. Cria-se primeiro um índice não-único
            // equivalente, só depois se apaga o antigo, para nunca ficar sem
            // índice a suportar a FK.
            migrationBuilder.CreateIndex(
                name: "IX_mapa_timesheet_faturas_ProjectId_Year_Month_tmp",
                table: "mapa_timesheet_faturas",
                columns: new[] { "ProjectId", "Year", "Month" });

            migrationBuilder.DropIndex(
                name: "IX_mapa_timesheet_faturas_ProjectId_Year_Month",
                table: "mapa_timesheet_faturas");

            migrationBuilder.RenameIndex(
                table: "mapa_timesheet_faturas",
                name: "IX_mapa_timesheet_faturas_ProjectId_Year_Month_tmp",
                newName: "IX_mapa_timesheet_faturas_ProjectId_Year_Month");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnuladaEm",
                table: "mapa_timesheet_faturas");

            migrationBuilder.DropColumn(
                name: "JustificativaAnulacao",
                table: "mapa_timesheet_faturas");

            migrationBuilder.CreateIndex(
                name: "IX_mapa_timesheet_faturas_ProjectId_Year_Month_tmp",
                table: "mapa_timesheet_faturas",
                columns: new[] { "ProjectId", "Year", "Month" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_mapa_timesheet_faturas_ProjectId_Year_Month",
                table: "mapa_timesheet_faturas");

            migrationBuilder.RenameIndex(
                table: "mapa_timesheet_faturas",
                name: "IX_mapa_timesheet_faturas_ProjectId_Year_Month_tmp",
                newName: "IX_mapa_timesheet_faturas_ProjectId_Year_Month");
        }
    }
}
