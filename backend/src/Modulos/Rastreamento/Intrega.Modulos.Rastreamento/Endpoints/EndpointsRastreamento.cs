using Intrega.Infraestrutura.Web;
using Intrega.Modulos.Rastreamento.Aplicacao;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Intrega.Modulos.Rastreamento.Endpoints;

internal static class EndpointsRastreamento
{
    public static void Mapear(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/rastreamento").WithTags("Rastreamento e jornada").RequireAuthorization().ComValidacao();

        g.MapPost("/posicoes", async (LoteDePosicoesRequisicao req, ServicoDeRastreamento s, CancellationToken ct) =>
                (await s.RegistrarPosicoesAsync(req, ct)).ParaHttp())
            .WithSummary("Recebe posições GPS em lote (o app acumula enquanto está sem internet).");

        g.MapGet("/resumo", async (DateOnly? de, DateOnly? ate, ServicoDeRastreamento s, CancellationToken ct) =>
                (await s.ResumirAsync(de, ate, ct)).ParaHttp())
            .WithSummary("Entregas, km rodados, horas trabalhadas, ganhos e custos do período.");

        g.MapGet("/configuracao-financeira", async (ServicoDeRastreamento s, CancellationToken ct) =>
            Results.Ok(await s.ObterConfiguracaoFinanceiraAsync(ct)));
        g.MapPut("/configuracao-financeira", async (ConfiguracaoFinanceiraDto dto, ServicoDeRastreamento s, CancellationToken ct) =>
        {
            await s.SalvarConfiguracaoFinanceiraAsync(dto, ct);
            return Results.NoContent();
        });

        g.MapGet("/frota/ao-vivo", async (ServicoDeRastreamento s, CancellationToken ct) =>
                (await s.FrotaAoVivoAsync(ct)).ParaHttp())
            .WithSummary("Última posição de cada entregador (gestor, plano Frota).");
    }
}
