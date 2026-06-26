using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MapaMensal.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "mapa_appconfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Key = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Value = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mapa_appconfigs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "mapa_categorias_contas_pessoais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Nome = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Cor = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Ordem = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mapa_categorias_contas_pessoais", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "mapa_contas_pessoais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Descricao = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Categoria = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DataVencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    DataPagamento = table.Column<DateOnly>(type: "date", nullable: true),
                    ValorPrevisto = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    ValorPago = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    Pago = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    GrupoRecorrencia = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    RecorrenciaAtual = table.Column<int>(type: "int", nullable: false),
                    TotalRecorrencias = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mapa_contas_pessoais", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "mapa_expenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Amount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mapa_expenses", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "mapa_holidays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsNational = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mapa_holidays", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "mapa_projects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Client = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DailyRate = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mapa_projects", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "mapa_users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Username = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PasswordHash = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Role = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mapa_users", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "mapa_tarefas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Titulo = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Descricao = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DataEntrega = table.Column<DateOnly>(type: "date", nullable: true),
                    HorasGastas = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Arquivado = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mapa_tarefas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mapa_tarefas_mapa_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "mapa_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "mapa_workdays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Mark = table.Column<decimal>(type: "decimal(65,30)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mapa_workdays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mapa_workdays_mapa_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "mapa_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "mapa_appconfigs",
                columns: new[] { "Id", "Key", "Value" },
                values: new object[] { 1, "IvaRate", "0.23" });

            migrationBuilder.InsertData(
                table: "mapa_categorias_contas_pessoais",
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

            migrationBuilder.InsertData(
                table: "mapa_holidays",
                columns: new[] { "Id", "Date", "IsNational", "Name" },
                values: new object[,]
                {
                    { 1, new DateOnly(2026, 6, 4), true, "Corpo de Deus" },
                    { 2, new DateOnly(2026, 6, 10), true, "Dia de Portugal" },
                    { 3, new DateOnly(2026, 8, 15), true, "Assunção de Nossa Senhora" },
                    { 4, new DateOnly(2026, 10, 5), true, "Implantação da República" },
                    { 5, new DateOnly(2026, 11, 1), true, "Todos os Santos" },
                    { 6, new DateOnly(2026, 12, 1), true, "Restauração da Independência" },
                    { 7, new DateOnly(2026, 12, 8), true, "Imaculada Conceição" },
                    { 8, new DateOnly(2026, 12, 25), true, "Natal" }
                });

            migrationBuilder.InsertData(
                table: "mapa_projects",
                columns: new[] { "Id", "Client", "DailyRate", "IsActive", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 1, null, 220m, true, "KCSIT/FNAC", 1 },
                    { 2, null, 242m, true, "CLOSER/NB", 2 },
                    { 3, null, 204m, true, "Capgemini/DPD", 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_mapa_appconfigs_Key",
                table: "mapa_appconfigs",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mapa_holidays_Date",
                table: "mapa_holidays",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_mapa_tarefas_ProjectId",
                table: "mapa_tarefas",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_mapa_users_Email",
                table: "mapa_users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mapa_users_Username",
                table: "mapa_users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mapa_workdays_ProjectId_Date",
                table: "mapa_workdays",
                columns: new[] { "ProjectId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mapa_appconfigs");

            migrationBuilder.DropTable(
                name: "mapa_categorias_contas_pessoais");

            migrationBuilder.DropTable(
                name: "mapa_contas_pessoais");

            migrationBuilder.DropTable(
                name: "mapa_expenses");

            migrationBuilder.DropTable(
                name: "mapa_holidays");

            migrationBuilder.DropTable(
                name: "mapa_tarefas");

            migrationBuilder.DropTable(
                name: "mapa_users");

            migrationBuilder.DropTable(
                name: "mapa_workdays");

            migrationBuilder.DropTable(
                name: "mapa_projects");
        }
    }
}
