using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MapaMensal.Migrations
{
    /// <inheritdoc />
    public partial class AddMoedaContaPessoal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Moeda",
                table: "mapa_contas_pessoais",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Observacoes",
                table: "mapa_contas_pessoais",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "ValorOriginal",
                table: "mapa_contas_pessoais",
                type: "decimal(65,30)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Moeda",
                table: "mapa_contas_pessoais");

            migrationBuilder.DropColumn(
                name: "Observacoes",
                table: "mapa_contas_pessoais");

            migrationBuilder.DropColumn(
                name: "ValorOriginal",
                table: "mapa_contas_pessoais");
        }
    }
}
