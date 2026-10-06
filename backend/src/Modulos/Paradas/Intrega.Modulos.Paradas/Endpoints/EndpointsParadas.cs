using Intrega.Infraestrutura.Web;
using Intrega.Modulos.Paradas.Aplicacao;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Intrega.Modulos.Paradas.Endpoints;

internal static class EndpointsParadas
{
    public static void Mapear(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/paradas").WithTags("Paradas (destinos)").RequireAuthorization().ComValidacao();

        g.MapGet("/", async (Guid? rotaId, bool? semRota, ServicoDeParadas s, CancellationToken ct) =>
                Results.Ok(await s.ListarAsync(rotaId, semRota == true, ct)))
            .WithSummary("Lista as paradas de uma rota, ou as paradas sem rota (semRota=true).");

        g.MapPost("/", async (CriarParadaRequisicao req, ServicoDeParadas s, CancellationToken ct) =>
            (await s.CriarAsync(req, ct)).ParaHttp(p => Results.Created($"/api/paradas/{p.Id}", p)));

        g.MapGet("/{id:guid}", async (Guid id, ServicoDeParadas s, CancellationToken ct) =>
            (await s.ObterAsync(id, ct)).ParaHttp());

        g.MapPut("/{id:guid}", async (Guid id, DadosDaParadaRequisicao req, ServicoDeParadas s, CancellationToken ct) =>
            (await s.AtualizarAsync(id, req, ct)).ParaHttp());

        g.MapDelete("/{id:guid}", async (Guid id, ServicoDeParadas s, CancellationToken ct) =>
            (await s.ExcluirAsync(id, ct)).ParaHttp());

        g.MapPut("/{id:guid}/local", async (Guid id, ConfirmarLocalRequisicao req, ServicoDeParadas s, CancellationToken ct) =>
                (await s.ConfirmarLocalAsync(id, req, ct)).ParaHttp())
            .WithSummary("Confirma/corrige o pino no mapa (vale para as próximas entregas no mesmo endereço).");

        g.MapPost("/{id:guid}/entregar", async (Guid id, EntregarRequisicao req, ServicoDeParadas s, CancellationToken ct) =>
            (await s.EntregarAsync(id, req, ct)).ParaHttp());

        g.MapPost("/{id:guid}/nao-entregue", async (Guid id, RegistrarFalhaRequisicao req, ServicoDeParadas s, CancellationToken ct) =>
                (await s.RegistrarFalhaAsync(id, req, ct)).ParaHttp())
            .WithSummary("Destinatário ausente/recusou. Recalcula a rota conforme a estratégia escolhida.");

        g.MapPost("/{id:guid}/reabrir", async (Guid id, ServicoDeParadas s, CancellationToken ct) =>
            (await s.ReabrirAsync(id, ct)).ParaHttp());

        g.MapPost("/{id:guid}/mover", async (Guid id, MoverParadaRequisicao req, ServicoDeParadas s, CancellationToken ct) =>
            (await s.MoverAsync(id, req, ct)).ParaHttp());

        g.MapPut("/{id:guid}/comprovante", async (Guid id, IFormFile foto, ServicoDeParadas s, CancellationToken ct) =>
            {
                await using var conteudo = foto.OpenReadStream();
                return (await s.EnviarComprovanteAsync(id, conteudo, foto.Length, foto.ContentType, ct)).ParaHttp();
            })
            .DisableAntiforgery()
            .WithSummary("Foto de comprovante de entrega (Pro).");

        g.MapGet("/{id:guid}/comprovante", async (Guid id, ServicoDeParadas s, CancellationToken ct) =>
            await s.AbrirComprovanteAsync(id, ct) is { } conteudo ? Results.Stream(conteudo, "image/jpeg") : Results.NotFound());
    }
}
