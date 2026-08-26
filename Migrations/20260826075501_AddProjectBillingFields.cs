using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MapaMensal.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectBillingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FaturacaoCodigoPostal",
                table: "mapa_projects",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "FaturacaoLocalidade",
                table: "mapa_projects",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "FaturacaoMorada",
                table: "mapa_projects",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "FaturacaoNif",
                table: "mapa_projects",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "FaturacaoNomeFiscal",
                table: "mapa_projects",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "FaturacaoPais",
                table: "mapa_projects",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "mapa_projects",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FaturacaoCodigoPostal", "FaturacaoLocalidade", "FaturacaoMorada", "FaturacaoNif", "FaturacaoNomeFiscal", "FaturacaoPais" },
                values: new object[] { null, null, null, null, null, "PT" });

            migrationBuilder.UpdateData(
                table: "mapa_projects",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "FaturacaoCodigoPostal", "FaturacaoLocalidade", "FaturacaoMorada", "FaturacaoNif", "FaturacaoNomeFiscal", "FaturacaoPais" },
                values: new object[] { null, null, null, null, null, "PT" });

            migrationBuilder.UpdateData(
                table: "mapa_projects",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "FaturacaoCodigoPostal", "FaturacaoLocalidade", "FaturacaoMorada", "FaturacaoNif", "FaturacaoNomeFiscal", "FaturacaoPais" },
                values: new object[] { null, null, null, null, null, "PT" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FaturacaoCodigoPostal",
                table: "mapa_projects");

            migrationBuilder.DropColumn(
                name: "FaturacaoLocalidade",
                table: "mapa_projects");

            migrationBuilder.DropColumn(
                name: "FaturacaoMorada",
                table: "mapa_projects");

            migrationBuilder.DropColumn(
                name: "FaturacaoNif",
                table: "mapa_projects");

            migrationBuilder.DropColumn(
                name: "FaturacaoNomeFiscal",
                table: "mapa_projects");

            migrationBuilder.DropColumn(
                name: "FaturacaoPais",
                table: "mapa_projects");
        }
    }
}
