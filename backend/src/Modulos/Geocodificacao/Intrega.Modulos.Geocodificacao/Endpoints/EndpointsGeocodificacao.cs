using Intrega.Modulos.Geocodificacao.Contratos;
using Intrega.Nucleo.Geo;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Intrega.Modulos.Geocodificacao.Endpoints;

internal static class EndpointsGeocodificacao
{
    public static void Mapear(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/geocodificacao").WithTags("Geocodificação").RequireAuthorization();

        g.MapGet("/cep/{cep}", async (string cep, IModuloGeocodificacao geo, CancellationToken ct) =>
            await geo.ConsultarCepAsync(cep, ct) is { } info ? Results.Ok(info) : Results.NotFound());

        g.MapGet("/buscar", async (string texto, double? lat, double? lng, IModuloGeocodificacao geo, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(texto) || texto.Trim().Length < 3)
                    return Results.Ok(Array.Empty<SugestaoDeEndereco>());
                PontoGeo? proximoDe = lat is { } la && lng is { } lo ? new PontoGeo(la, lo) : null;
                return Results.Ok(await geo.BuscarAsync(texto.Trim(), proximoDe, ct));
            })
            .WithSummary("Autocompletar endereço (Photon). Também aceita CEP.");

        g.MapGet("/reverso", async (double lat, double lng, IModuloGeocodificacao geo, CancellationToken ct) =>
            await geo.GeocodificacaoReversaAsync(new PontoGeo(lat, lng), ct) is { } e ? Results.Ok(e) : Results.NotFound());

        g.MapPost("/geocodificar", async (EnderecoInformado endereco, IModuloGeocodificacao geo, CancellationToken ct) =>
            await geo.GeocodificarAsync(endereco, null, ct) is { } r ? Results.Ok(r) : Results.NotFound());
    }
}
