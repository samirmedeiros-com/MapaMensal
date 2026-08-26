using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MapaMensal.Migrations
{
    /// <inheritdoc />
    public partial class AddPrazoVencimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PrazoVencimentoDias",
                table: "mapa_projects",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "mapa_projects",
                keyColumn: "Id",
                keyValue: 1,
                column: "PrazoVencimentoDias",
                value: 30);

            migrationBuilder.UpdateData(
                table: "mapa_projects",
                keyColumn: "Id",
                keyValue: 2,
                column: "PrazoVencimentoDias",
                value: 30);

            migrationBuilder.UpdateData(
                table: "mapa_projects",
                keyColumn: "Id",
                keyValue: 3,
                column: "PrazoVencimentoDias",
                value: 30);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrazoVencimentoDias",
                table: "mapa_projects");
        }
    }
}
