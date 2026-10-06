using Intrega.Infraestrutura.Web;
using Intrega.Modulos.Etiquetas.Aplicacao;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Intrega.Modulos.Etiquetas.Endpoints;

internal static class EndpointsEtiquetas
{
    public static void Mapear(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/etiquetas").WithTags("Leitura de etiquetas").RequireAuthorization().ComValidacao();

        g.MapPost("/interpretar", async (LeituraDeEtiquetaRequisicao req, ServicoDeEtiquetas s, CancellationToken ct) =>
                Results.Ok(await s.InterpretarAsync(req, ct)))
            .WithSummary("Extrai destinatário, endereço e código do texto (OCR) e dos códigos lidos. Não cria nada.");

        g.MapPost("/paradas", async (CriarParadaDaEtiquetaRequisicao req, ServicoDeEtiquetas s, CancellationToken ct) =>
                (await s.CriarParadaAsync(req, ct)).ParaHttp())
            .WithSummary("Cria a parada a partir da etiqueta (não duplica pacote já lido).");
    }
}
