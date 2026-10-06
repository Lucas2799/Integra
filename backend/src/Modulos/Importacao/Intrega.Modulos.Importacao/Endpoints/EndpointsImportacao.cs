using System.Text;
using Intrega.Infraestrutura.Web;
using Intrega.Modulos.Importacao.Aplicacao;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Intrega.Modulos.Importacao.Endpoints;

internal static class EndpointsImportacao
{
    public static void Mapear(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/importacoes").WithTags("Importação de planilha").RequireAuthorization();

        g.MapPost("/pre-visualizar", (IFormFile arquivo, ServicoDeImportacao s) =>
            {
                if (arquivo.Length > ServicoDeImportacao.TamanhoMaximoDoArquivo) return ArquivoGrande();
                using var conteudo = arquivo.OpenReadStream();
                return s.PreVisualizar(conteudo, arquivo.FileName).ParaHttp();
            })
            .DisableAntiforgery()
            .WithSummary("Mostra como as colunas da planilha foram reconhecidas, sem criar paradas.");

        g.MapPost("/", async (IFormFile arquivo, Guid? rotaId, ServicoDeImportacao s, CancellationToken ct) =>
            {
                if (arquivo.Length > ServicoDeImportacao.TamanhoMaximoDoArquivo) return ArquivoGrande();
                await using var conteudo = arquivo.OpenReadStream();
                return (await s.ImportarAsync(conteudo, arquivo.FileName, rotaId, ct))
                    .ParaHttp(t => Results.Accepted($"/api/importacoes/{t.Id}", t));
            })
            .DisableAntiforgery()
            .WithSummary("Importa a planilha (Pro). O processamento acontece em segundo plano.");

        g.MapGet("/", async (ServicoDeImportacao s, CancellationToken ct) => Results.Ok(await s.ListarRecentesAsync(ct)));

        g.MapGet("/{id:guid}", async (Guid id, ServicoDeImportacao s, CancellationToken ct) =>
            (await s.ObterAsync(id, ct)).ParaHttp());

        g.MapGet("/modelo", () => Results.File(
                Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(ServicoDeImportacao.ModeloCsv())).ToArray(),
                "text/csv", "modelo-intrega.csv"))
            .AllowAnonymous();
    }

    private static IResult ArquivoGrande() =>
        Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge, title: "importacao.arquivo_grande",
            detail: "O arquivo deve ter até 5 MB.");
}
