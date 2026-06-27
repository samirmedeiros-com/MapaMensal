using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MapaMensal.Migrations
{
    /// <inheritdoc />
    public partial class AceitarConvite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF NOT EXISTS protege contra re-execução parcial (colunas já criadas)
            migrationBuilder.Sql(
                "ALTER TABLE mapa_compromisso_participantes " +
                "ADD COLUMN IF NOT EXISTS Aceite tinyint(1) NOT NULL DEFAULT FALSE");

            migrationBuilder.Sql(
                "ALTER TABLE mapa_compromisso_participantes " +
                "ADD COLUMN IF NOT EXISTS AceiteEm datetime(6) NULL");

            migrationBuilder.Sql(
                "ALTER TABLE mapa_compromisso_participantes " +
                "ADD COLUMN IF NOT EXISTS Token varchar(32) NOT NULL DEFAULT (REPLACE(UUID(), '-', ''))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE mapa_compromisso_participantes DROP COLUMN IF EXISTS Token");

            migrationBuilder.Sql(
                "ALTER TABLE mapa_compromisso_participantes DROP COLUMN IF EXISTS AceiteEm");

            migrationBuilder.Sql(
                "ALTER TABLE mapa_compromisso_participantes DROP COLUMN IF EXISTS Aceite");
        }
    }
}
