using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MapaMensal.Migrations
{
    /// <inheritdoc />
    public partial class AddFaturacaoEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FaturacaoEmail",
                table: "mapa_projects",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "mapa_projects",
                keyColumn: "Id",
                keyValue: 1,
                column: "FaturacaoEmail",
                value: null);

            migrationBuilder.UpdateData(
                table: "mapa_projects",
                keyColumn: "Id",
                keyValue: 2,
                column: "FaturacaoEmail",
                value: null);

            migrationBuilder.UpdateData(
                table: "mapa_projects",
                keyColumn: "Id",
                keyValue: 3,
                column: "FaturacaoEmail",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FaturacaoEmail",
                table: "mapa_projects");
        }
    }
}
