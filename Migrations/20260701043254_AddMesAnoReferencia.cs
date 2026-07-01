using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MapaMensal.Migrations
{
    /// <inheritdoc />
    public partial class AddMesAnoReferencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AnoReferencia",
                table: "mapa_contas_pessoais",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MesReferencia",
                table: "mapa_contas_pessoais",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            // Backfill: para registos existentes, a competência deriva do DataVencimento.
            // Aspas obrigatórias: o Oracle EF Provider criou a tabela/colunas com quoted identifiers (case-sensitive).
            migrationBuilder.Sql(
                """
                UPDATE "mapa_contas_pessoais"
                SET "AnoReferencia"  = TO_NUMBER(SUBSTR("DataVencimento", 1, 4)),
                    "MesReferencia"  = TO_NUMBER(SUBSTR("DataVencimento", 6, 2))
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnoReferencia",
                table: "mapa_contas_pessoais");

            migrationBuilder.DropColumn(
                name: "MesReferencia",
                table: "mapa_contas_pessoais");
        }
    }
}
