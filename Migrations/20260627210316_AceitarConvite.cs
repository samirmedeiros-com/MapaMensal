using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MapaMensal.Migrations
{
    /// <inheritdoc />
    public partial class AceitarConvite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Aceite",
                table: "mapa_compromisso_participantes",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "AceiteEm",
                table: "mapa_compromisso_participantes",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Token",
                table: "mapa_compromisso_participantes",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValueSql: "(REPLACE(UUID(), '-', ''))")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Aceite",
                table: "mapa_compromisso_participantes");

            migrationBuilder.DropColumn(
                name: "AceiteEm",
                table: "mapa_compromisso_participantes");

            migrationBuilder.DropColumn(
                name: "Token",
                table: "mapa_compromisso_participantes");
        }
    }
}
