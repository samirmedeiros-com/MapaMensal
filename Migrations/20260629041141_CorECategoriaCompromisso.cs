using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MapaMensal.Migrations
{
    /// <inheritdoc />
    public partial class CorECategoriaCompromisso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CategoriaId",
                table: "mapa_compromissos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cor",
                table: "mapa_compromissos",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "mapa_categorias_compromisso",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Nome = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Cor = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mapa_categorias_compromisso", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_mapa_compromissos_CategoriaId",
                table: "mapa_compromissos",
                column: "CategoriaId");

            migrationBuilder.AddForeignKey(
                name: "FK_mapa_compromissos_mapa_categorias_compromisso_CategoriaId",
                table: "mapa_compromissos",
                column: "CategoriaId",
                principalTable: "mapa_categorias_compromisso",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_mapa_compromissos_mapa_categorias_compromisso_CategoriaId",
                table: "mapa_compromissos");

            migrationBuilder.DropTable(
                name: "mapa_categorias_compromisso");

            migrationBuilder.DropIndex(
                name: "IX_mapa_compromissos_CategoriaId",
                table: "mapa_compromissos");

            migrationBuilder.DropColumn(
                name: "CategoriaId",
                table: "mapa_compromissos");

            migrationBuilder.DropColumn(
                name: "Cor",
                table: "mapa_compromissos");
        }
    }
}
