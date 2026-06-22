using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MapaMensal.Migrations
{
    /// <inheritdoc />
    public partial class AddContasPessoais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContasPessoais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Descricao = table.Column<string>(type: "TEXT", nullable: false),
                    Categoria = table.Column<string>(type: "TEXT", nullable: false),
                    DataVencimento = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DataPagamento = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ValorPrevisto = table.Column<decimal>(type: "TEXT", nullable: false),
                    ValorPago = table.Column<decimal>(type: "TEXT", nullable: true),
                    Pago = table.Column<bool>(type: "INTEGER", nullable: false),
                    GrupoRecorrencia = table.Column<Guid>(type: "TEXT", nullable: true),
                    RecorrenciaAtual = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalRecorrencias = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContasPessoais", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContasPessoais");
        }
    }
}
