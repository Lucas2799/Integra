using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Intrega.Modulos.Rastreamento.Infraestrutura.Migracoes
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "rastreamento");

            migrationBuilder.CreateTable(
                name: "configuracoes_financeiras",
                schema: "rastreamento",
                columns: table => new
                {
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValorPorEntrega = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    ValorPorPacote = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    PrecoDoCombustivelPorLitro = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    KmPorLitro = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    CustoFixoPorDia = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuracoes_financeiras", x => x.UsuarioId);
                });

            migrationBuilder.CreateTable(
                name: "posicoes",
                schema: "rastreamento",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    RotaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    VelocidadeMetrosPorSegundo = table.Column<double>(type: "double precision", nullable: true),
                    Direcao = table.Column<double>(type: "double precision", nullable: true),
                    PrecisaoMetros = table.Column<double>(type: "double precision", nullable: true),
                    RegistradaEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_posicoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "registros_de_entrega",
                schema: "rastreamento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    RotaId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParadaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Resultado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Motivo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Marketplace = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    QuantidadeDePacotes = table.Column<int>(type: "integer", nullable: false),
                    OcorridoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registros_de_entrega", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_posicoes_RegistradaEm",
                schema: "rastreamento",
                table: "posicoes",
                column: "RegistradaEm");

            migrationBuilder.CreateIndex(
                name: "IX_posicoes_UsuarioId_RegistradaEm",
                schema: "rastreamento",
                table: "posicoes",
                columns: new[] { "UsuarioId", "RegistradaEm" });

            migrationBuilder.CreateIndex(
                name: "IX_registros_de_entrega_UsuarioId_OcorridoEm",
                schema: "rastreamento",
                table: "registros_de_entrega",
                columns: new[] { "UsuarioId", "OcorridoEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuracoes_financeiras",
                schema: "rastreamento");

            migrationBuilder.DropTable(
                name: "posicoes",
                schema: "rastreamento");

            migrationBuilder.DropTable(
                name: "registros_de_entrega",
                schema: "rastreamento");
        }
    }
}
