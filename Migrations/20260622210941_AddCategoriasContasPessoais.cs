using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MapaMensal.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoriasContasPessoais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategoriasContasPessoais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    Cor = table.Column<string>(type: "TEXT", nullable: true),
                    Ordem = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriasContasPessoais", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "CategoriasContasPessoais",
                columns: new[] { "Id", "Cor", "Nome", "Ordem" },
                values: new object[,]
                {
                    { 1, "#5c6bc0", "Habitação", 1 },
                    { 2, "#43a047", "Alimentação", 2 },
                    { 3, "#fb8c00", "Transporte", 3 },
                    { 4, "#e53935", "Saúde", 4 },
                    { 5, "#8e24aa", "Educação", 5 },
                    { 6, "#00897b", "Comunicações", 6 },
                    { 7, "#f4511e", "Lazer", 7 },
                    { 8, "#039be5", "Seguros", 8 },
                    { 9, "#6d4c41", "Assinaturas", 9 },
                    { 10, "#757575", "Outros", 10 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CategoriasContasPessoais");
        }
    }
}
