using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MapaMensal.Migrations
{
    /// <inheritdoc />
    public partial class AddTarefaArquivado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Arquivado",
                table: "Tarefas",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Arquivado",
                table: "Tarefas");
        }
    }
}
