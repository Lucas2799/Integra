using Intrega.Infraestrutura.Web;
using Intrega.Modulos.Roteirizacao.Aplicacao;
using Intrega.Nucleo.Geo;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Intrega.Modulos.Roteirizacao.Endpoints;

internal static class EndpointsRoteirizacao
{
    public static void Mapear(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/rotas").WithTags("Rotas").RequireAuthorization().ComValidacao();

        g.MapGet("/", async (DateOnly? de, DateOnly? ate, ServicoDeRotas s, CancellationToken ct) =>
            Results.Ok(await s.ListarAsync(de, ate, ct)));
        g.MapPost("/", async (ConfiguracaoDaRotaRequisicao req, ServicoDeRotas s, CancellationToken ct) =>
            (await s.CriarAsync(req, ct)).ParaHttp(r => Results.Created($"/api/rotas/{r.Resumo.Id}", r)));
        g.MapGet("/{id:guid}", async (Guid id, ServicoDeRotas s, CancellationToken ct) =>
            (await s.ObterAsync(id, ct)).ParaHttp());
        g.MapPut("/{id:guid}", async (Guid id, ConfiguracaoDaRotaRequisicao req, ServicoDeRotas s, CancellationToken ct) =>
            (await s.AlterarAsync(id, req, ct)).ParaHttp());
        g.MapDelete("/{id:guid}", async (Guid id, ServicoDeRotas s, CancellationToken ct) =>
            (await s.ExcluirAsync(id, ct)).ParaHttp());

        g.MapPost("/{id:guid}/otimizar", async (Guid id, PosicaoRequisicao req, ServicoDeRotas s, CancellationToken ct) =>
                (await s.OtimizarAsync(id, req, ct)).ParaHttp())
            .WithSummary("Calcula a melhor ordem das paradas (OR-Tools + OSRM).");
        g.MapPost("/{id:guid}/recalcular", async (Guid id, PosicaoRequisicao req, ServicoDeRotas s, CancellationToken ct) =>
                (await s.RecalcularAsync(id, req, ct)).ParaHttp())
            .WithSummary("Reotimiza as paradas que faltam a partir da posição atual.");
        g.MapPut("/{id:guid}/sequencia", async (Guid id, ReordenarRequisicao req, ServicoDeRotas s, CancellationToken ct) =>
                (await s.ReordenarAsync(id, req, ct)).ParaHttp())
            .WithSummary("Ordem definida à mão (arrastar e soltar).");
        g.MapPost("/{id:guid}/iniciar", async (Guid id, ServicoDeRotas s, CancellationToken ct) =>
            (await s.IniciarAsync(id, ct)).ParaHttp());
        g.MapPost("/{id:guid}/concluir", async (Guid id, ServicoDeRotas s, CancellationToken ct) =>
            (await s.ConcluirAsync(id, ct)).ParaHttp());

        g.MapGet("/{id:guid}/previsao-de-chegada", async (Guid id, double lat, double lng, ServicoDeRotas s, CancellationToken ct) =>
            (await s.PreverChegadasAsync(id, new PontoGeo(lat, lng), ct)).ParaHttp());
        g.MapGet("/{id:guid}/navegacao", async (Guid id, double lat, double lng, Guid? paradaId, ServicoDeRotas s, CancellationToken ct) =>
                (await s.NavegarAsync(id, new PontoGeo(lat, lng), paradaId, ct)).ParaHttp())
            .WithSummary("Passo a passo em português até a próxima parada (Pro).");
        g.MapGet("/{id:guid}/ordem-de-carregamento", async (Guid id, ServicoDeRotas s, CancellationToken ct) =>
                (await s.OrdemDeCarregamentoAsync(id, ct)).ParaHttp())
            .WithSummary("Ordem para carregar o veículo: a última entrega vai no fundo.");

        var frota = app.MapGroup("/api/frota").WithTags("Frota (B2B)").RequireAuthorization().ComValidacao();
        frota.MapPost("/despachar", async (DespachoRequisicao req, ServicoDeFrota s, CancellationToken ct) =>
                (await s.DespacharAsync(req, ct)).ParaHttp())
            .WithSummary("Distribui as paradas entre os entregadores (roteirização de vários veículos).");
        frota.MapGet("/rotas", async (DateOnly? data, ServicoDeFrota s, CancellationToken ct) =>
            (await s.ListarAsync(data, ct)).ParaHttp());
    }
}
