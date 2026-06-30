using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MapaMensal.Migrations
{
    /// <inheritdoc />
    public partial class InitialOracle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mapa_appconfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    Key = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    Value = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mapa_appconfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mapa_categorias_compromisso",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    Nome = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Cor = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mapa_categorias_compromisso", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mapa_categorias_contas_pessoais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    Nome = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Cor = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    Ordem = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mapa_categorias_contas_pessoais", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mapa_contas_pessoais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    Descricao = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Categoria = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    DataVencimento = table.Column<string>(type: "NVARCHAR2(10)", nullable: false),
                    DataPagamento = table.Column<string>(type: "NVARCHAR2(10)", nullable: true),
                    ValorPrevisto = table.Column<decimal>(type: "NUMBER(18,2)", nullable: false),
                    ValorPago = table.Column<decimal>(type: "NUMBER(18,2)", nullable: true),
                    Pago = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    MetodoPagamento = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    GrupoRecorrencia = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    RecorrenciaAtual = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    TotalRecorrencias = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mapa_contas_pessoais", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mapa_expenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    Year = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Month = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Category = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Amount = table.Column<decimal>(type: "NUMBER(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mapa_expenses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mapa_holidays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    Date = table.Column<string>(type: "NVARCHAR2(10)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    IsNational = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mapa_holidays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mapa_horarios_disponiveis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    DiaSemana = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    HoraInicio = table.Column<TimeSpan>(type: "INTERVAL DAY(8) TO SECOND(7)", nullable: false),
                    HoraFim = table.Column<TimeSpan>(type: "INTERVAL DAY(8) TO SECOND(7)", nullable: false),
                    DuracaoSlotMinutos = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Ativo = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mapa_horarios_disponiveis", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mapa_projects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    Name = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Client = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    DailyRate = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false),
                    IsActive = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mapa_projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mapa_users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    Username = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    Email = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    PasswordHash = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Role = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    IsActive = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mapa_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mapa_compromissos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    Titulo = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Descricao = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    Inicio = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    Fim = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ProjectId = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    ContaPessoalId = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    Local = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Online = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    LinkOnline = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    Tipo = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    NotificarParticipantes = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    RecorrenciaId = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    Cor = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    CategoriaId = table.Column<int>(type: "NUMBER(10)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mapa_compromissos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mapa_compromissos_mapa_categorias_compromisso_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "mapa_categorias_compromisso",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_mapa_compromissos_mapa_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "mapa_projects",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "mapa_tarefas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ProjectId = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Titulo = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Descricao = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    Status = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DataEntrega = table.Column<string>(type: "NVARCHAR2(10)", nullable: true),
                    HorasGastas = table.Column<decimal>(type: "NUMBER(18,2)", nullable: false),
                    Arquivado = table.Column<bool>(type: "BOOLEAN", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "mapa_workdays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ProjectId = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Date = table.Column<string>(type: "NVARCHAR2(10)", nullable: false),
                    Mark = table.Column<decimal>(type: "NUMBER(18,4)", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "mapa_compromisso_participantes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    CompromissoId = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Nome = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Email = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Telefone = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    CodigoPais = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    Notificar = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    Token = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    Aceite = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    AceiteEm = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mapa_compromisso_participantes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mapa_compromisso_participantes_mapa_compromissos_CompromissoId",
                        column: x => x.CompromissoId,
                        principalTable: "mapa_compromissos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                    { 1, "2026-06-04", true, "Corpo de Deus" },
                    { 2, "2026-06-10", true, "Dia de Portugal" },
                    { 3, "2026-08-15", true, "Assunção de Nossa Senhora" },
                    { 4, "2026-10-05", true, "Implantação da República" },
                    { 5, "2026-11-01", true, "Todos os Santos" },
                    { 6, "2026-12-01", true, "Restauração da Independência" },
                    { 7, "2026-12-08", true, "Imaculada Conceição" },
                    { 8, "2026-12-25", true, "Natal" }
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
                name: "IX_mapa_compromisso_participantes_CompromissoId",
                table: "mapa_compromisso_participantes",
                column: "CompromissoId");

            migrationBuilder.CreateIndex(
                name: "IX_mapa_compromissos_CategoriaId",
                table: "mapa_compromissos",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_mapa_compromissos_ProjectId",
                table: "mapa_compromissos",
                column: "ProjectId");

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
                name: "mapa_compromisso_participantes");

            migrationBuilder.DropTable(
                name: "mapa_contas_pessoais");

            migrationBuilder.DropTable(
                name: "mapa_expenses");

            migrationBuilder.DropTable(
                name: "mapa_holidays");

            migrationBuilder.DropTable(
                name: "mapa_horarios_disponiveis");

            migrationBuilder.DropTable(
                name: "mapa_tarefas");

            migrationBuilder.DropTable(
                name: "mapa_users");

            migrationBuilder.DropTable(
                name: "mapa_workdays");

            migrationBuilder.DropTable(
                name: "mapa_compromissos");

            migrationBuilder.DropTable(
                name: "mapa_categorias_compromisso");

            migrationBuilder.DropTable(
                name: "mapa_projects");
        }
    }
}
