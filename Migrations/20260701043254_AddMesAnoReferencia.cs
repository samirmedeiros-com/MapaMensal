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
            // Idempotente: ignora ORA-01430 (coluna já existe) caso a migration
            // tenha sido parcialmente aplicada numa tentativa anterior.
            migrationBuilder.Sql("""
                BEGIN
                  EXECUTE IMMEDIATE 'ALTER TABLE "mapa_contas_pessoais" ADD "AnoReferencia" NUMBER(10) DEFAULT 0 NOT NULL';
                EXCEPTION WHEN OTHERS THEN
                  IF SQLCODE != -1430 THEN RAISE; END IF;
                END;
                """);

            migrationBuilder.Sql("""
                BEGIN
                  EXECUTE IMMEDIATE 'ALTER TABLE "mapa_contas_pessoais" ADD "MesReferencia" NUMBER(10) DEFAULT 0 NOT NULL';
                EXCEPTION WHEN OTHERS THEN
                  IF SQLCODE != -1430 THEN RAISE; END IF;
                END;
                """);

            // Backfill: apenas linhas ainda por preencher (AnoReferencia = 0).
            // Aspas obrigatórias — Oracle EF Provider criou a tabela com quoted identifiers (case-sensitive).
            migrationBuilder.Sql("""
                UPDATE "mapa_contas_pessoais"
                SET "AnoReferencia" = TO_NUMBER(SUBSTR("DataVencimento", 1, 4)),
                    "MesReferencia" = TO_NUMBER(SUBSTR("DataVencimento", 6, 2))
                WHERE "AnoReferencia" = 0
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                BEGIN
                  EXECUTE IMMEDIATE 'ALTER TABLE "mapa_contas_pessoais" DROP COLUMN "AnoReferencia"';
                EXCEPTION WHEN OTHERS THEN
                  IF SQLCODE != -904 THEN RAISE; END IF;
                END;
                """);

            migrationBuilder.Sql("""
                BEGIN
                  EXECUTE IMMEDIATE 'ALTER TABLE "mapa_contas_pessoais" DROP COLUMN "MesReferencia"';
                EXCEPTION WHEN OTHERS THEN
                  IF SQLCODE != -904 THEN RAISE; END IF;
                END;
                """);
        }
    }
}
