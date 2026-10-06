using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intrega.Modulos.Roteirizacao.Infraestrutura.Migracoes
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "roteirizacao");

            migrationBuilder.CreateTable(
                name: "rotas",
                schema: "roteirizacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DonoId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntregadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizacaoId = table.Column<Guid>(type: "uuid", nullable: true),
                    Nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Veiculo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LatitudeDeSaida = table.Column<double>(type: "double precision", nullable: false),
                    LongitudeDeSaida = table.Column<double>(type: "double precision", nullable: false),
                    DescricaoDaSaida = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    LatitudeDeChegada = table.Column<double>(type: "double precision", nullable: true),
                    LongitudeDeChegada = table.Column<double>(type: "double precision", nullable: true),
                    DescricaoDaChegada = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    VoltarAoInicio = table.Column<bool>(type: "boolean", nullable: false),
                    HorarioPlanejadoDeSaida = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IniciadaEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcluidaEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OtimizadaEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SaidaConsiderada = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AlgoritmoUsado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    MotorUsado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DistanciaTotalMetros = table.Column<double>(type: "double precision", nullable: false),
                    DuracaoTotalSegundos = table.Column<double>(type: "double precision", nullable: false),
                    Geometria = table.Column<string>(type: "text", nullable: true),
                    Recalculos = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sequencia = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rotas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rotas_DonoId_Data",
                schema: "roteirizacao",
                table: "rotas",
                columns: new[] { "DonoId", "Data" });

            migrationBuilder.CreateIndex(
                name: "IX_rotas_EntregadorId_Data",
                schema: "roteirizacao",
                table: "rotas",
                columns: new[] { "EntregadorId", "Data" });

            migrationBuilder.CreateIndex(
                name: "IX_rotas_OrganizacaoId_Data",
                schema: "roteirizacao",
                table: "rotas",
                columns: new[] { "OrganizacaoId", "Data" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rotas",
                schema: "roteirizacao");
        }
    }
}
