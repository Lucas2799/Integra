using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intrega.Modulos.Importacao.Infraestrutura.Migracoes
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "importacao");

            migrationBuilder.CreateTable(
                name: "trabalhos",
                schema: "importacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizacaoId = table.Column<Guid>(type: "uuid", nullable: true),
                    RotaId = table.Column<Guid>(type: "uuid", nullable: true),
                    NomeDoArquivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TotalDeLinhas = table.Column<int>(type: "integer", nullable: false),
                    LinhasProcessadas = table.Column<int>(type: "integer", nullable: false),
                    ParadasCriadas = table.Column<int>(type: "integer", nullable: false),
                    PrecisamConfirmacao = table.Column<int>(type: "integer", nullable: false),
                    SemLocalizacao = table.Column<int>(type: "integer", nullable: false),
                    LinhasPendentes = table.Column<string>(type: "jsonb", nullable: false),
                    ConcluidaEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    erros = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trabalhos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_trabalhos_Status",
                schema: "importacao",
                table: "trabalhos",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_trabalhos_UsuarioId_CriadoEm",
                schema: "importacao",
                table: "trabalhos",
                columns: new[] { "UsuarioId", "CriadoEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trabalhos",
                schema: "importacao");
        }
    }
}
