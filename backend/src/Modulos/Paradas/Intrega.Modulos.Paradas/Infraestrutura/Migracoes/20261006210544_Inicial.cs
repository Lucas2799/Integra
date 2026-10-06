using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intrega.Modulos.Paradas.Infraestrutura.Migracoes
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "paradas");

            migrationBuilder.CreateTable(
                name: "paradas",
                schema: "paradas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponsavelId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizacaoId = table.Column<Guid>(type: "uuid", nullable: true),
                    RotaId = table.Column<Guid>(type: "uuid", nullable: true),
                    logradouro = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    complemento = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    bairro = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    cidade = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    cep = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    ConfiancaDaLocalizacao = table.Column<double>(type: "double precision", nullable: true),
                    ProvedorDaLocalizacao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    LocalConfirmado = table.Column<bool>(type: "boolean", nullable: false),
                    NomeDoDestinatario = table.Column<string>(type: "text", nullable: true),
                    TelefoneDoDestinatario = table.Column<string>(type: "text", nullable: true),
                    Observacoes = table.Column<string>(type: "text", nullable: true),
                    CodigosDePacote = table.Column<List<string>>(type: "text[]", nullable: false),
                    QuantidadeDePacotes = table.Column<int>(type: "integer", nullable: false),
                    Marketplace = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    JanelaInicio = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    JanelaFim = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Prioridade = table.Column<int>(type: "integer", nullable: false),
                    TempoDeAtendimentoSegundos = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Tentativas = table.Column<int>(type: "integer", nullable: false),
                    UltimoMotivoDeFalha = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    EstrategiaDeNovaTentativa = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    NovaTentativaApos = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EntregueEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LatitudeDaEntrega = table.Column<double>(type: "double precision", nullable: true),
                    LongitudeDaEntrega = table.Column<double>(type: "double precision", nullable: true),
                    RecebidoPor = table.Column<string>(type: "text", nullable: true),
                    ObservacoesDaEntrega = table.Column<string>(type: "text", nullable: true),
                    CaminhoDoComprovante = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paradas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_paradas_CodigosDePacote",
                schema: "paradas",
                table: "paradas",
                column: "CodigosDePacote")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_paradas_OrganizacaoId_Status",
                schema: "paradas",
                table: "paradas",
                columns: new[] { "OrganizacaoId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_paradas_ResponsavelId_Status",
                schema: "paradas",
                table: "paradas",
                columns: new[] { "ResponsavelId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_paradas_RotaId",
                schema: "paradas",
                table: "paradas",
                column: "RotaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "paradas",
                schema: "paradas");
        }
    }
}
