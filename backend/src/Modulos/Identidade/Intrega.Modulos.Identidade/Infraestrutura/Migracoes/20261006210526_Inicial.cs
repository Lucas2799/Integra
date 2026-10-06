using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intrega.Modulos.Identidade.Infraestrutura.Migracoes
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identidade");

            migrationBuilder.CreateTable(
                name: "compras",
                schema: "identidade",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Plataforma = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProdutoId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HashDoComprovante = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Plano = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExpiraEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compras", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "organizacoes",
                schema: "identidade",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DonoId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodigoDeConvite = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PlanoExpiraEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizacoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "registros_de_uso",
                schema: "identidade",
                columns: table => new
                {
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Recurso = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Dia = table.Column<DateOnly>(type: "date", nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registros_de_uso", x => new { x.UsuarioId, x.Recurso, x.Dia });
                });

            migrationBuilder.CreateTable(
                name: "tokens_de_atualizacao",
                schema: "identidade",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    HashDoToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiraEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevogadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tokens_de_atualizacao", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                schema: "identidade",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    HashDaSenha = table.Column<string>(type: "text", nullable: false),
                    Telefone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Plano = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PlanoExpiraEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EmPeriodoDeTeste = table.Column<bool>(type: "boolean", nullable: false),
                    OrganizacaoId = table.Column<Guid>(type: "uuid", nullable: true),
                    PapelNaOrganizacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_compras_HashDoComprovante",
                schema: "identidade",
                table: "compras",
                column: "HashDoComprovante",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organizacoes_CodigoDeConvite",
                schema: "identidade",
                table: "organizacoes",
                column: "CodigoDeConvite",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tokens_de_atualizacao_HashDoToken",
                schema: "identidade",
                table: "tokens_de_atualizacao",
                column: "HashDoToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tokens_de_atualizacao_UsuarioId",
                schema: "identidade",
                table: "tokens_de_atualizacao",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_Email",
                schema: "identidade",
                table: "usuarios",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_OrganizacaoId",
                schema: "identidade",
                table: "usuarios",
                column: "OrganizacaoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "compras",
                schema: "identidade");

            migrationBuilder.DropTable(
                name: "organizacoes",
                schema: "identidade");

            migrationBuilder.DropTable(
                name: "registros_de_uso",
                schema: "identidade");

            migrationBuilder.DropTable(
                name: "tokens_de_atualizacao",
                schema: "identidade");

            migrationBuilder.DropTable(
                name: "usuarios",
                schema: "identidade");
        }
    }
}
