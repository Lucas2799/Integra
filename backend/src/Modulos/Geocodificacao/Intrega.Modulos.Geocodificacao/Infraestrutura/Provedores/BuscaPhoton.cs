using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Intrega.Modulos.Geocodificacao.Aplicacao;
using Intrega.Modulos.Geocodificacao.Contratos;
using Intrega.Nucleo.Geo;

namespace Intrega.Modulos.Geocodificacao.Infraestrutura.Provedores;

/// <summary>Photon (komoot): busca com suporte a autocompletar sobre dados do OpenStreetMap, gratuita (uso justo).</summary>
internal sealed class BuscaPhoton(HttpClient http) : IProvedorDeBusca
{
    public async Task<IReadOnlyList<SugestaoDeEndereco>> BuscarAsync(string texto, PontoGeo? proximoDe, CancellationToken ct)
    {
        var url = $"api/?limit=8&q={Uri.EscapeDataString(texto)}";
        if (proximoDe is { } p) url += FormattableString.Invariant($"&lat={p.Latitude}&lon={p.Longitude}");

        using var resposta = await http.GetAsync(url, ct);
        if (!resposta.IsSuccessStatusCode) return [];
        var corpo = await resposta.Content.ReadFromJsonAsync<RespostaPhoton>(ct);
        if (corpo?.Resultados is null) return [];

        return corpo.Resultados
            .Where(f => f.Propriedades?.CodigoDoPais?.Equals("BR", StringComparison.OrdinalIgnoreCase) == true
                        && f.Geometria?.Coordenadas is { Length: 2 })
            .Select(f =>
            {
                var pr = f.Propriedades!;
                var rua = pr.Rua ?? (pr.TipoOsm == "highway" ? pr.Nome : null);
                var uf = UfsBrasileiras.ParaUf(pr.Estado);
                var descricao = NormalizadorDeEndereco.MontarDescricao(rua ?? pr.Nome, pr.Numero, null, pr.Bairro, pr.Cidade, uf);
                var endereco = new EnderecoNormalizado(rua ?? pr.Nome, pr.Numero, null, pr.Bairro, pr.Cidade, uf,
                    NormalizadorDeEndereco.NormalizarCep(pr.Cep), descricao);
                // O GeoJSON vem como [longitude, latitude].
                var local = new PontoGeo(f.Geometria!.Coordenadas![1], f.Geometria.Coordenadas[0]);
                return new SugestaoDeEndereco(descricao, endereco, local);
            })
            .Take(5)
            .ToList();
    }

    private sealed record RespostaPhoton([property: JsonPropertyName("features")] List<ResultadoPhoton>? Resultados);

    private sealed record ResultadoPhoton(
        [property: JsonPropertyName("geometry")] GeometriaPhoton? Geometria,
        [property: JsonPropertyName("properties")] PropriedadesPhoton? Propriedades);

    private sealed record GeometriaPhoton([property: JsonPropertyName("coordinates")] double[]? Coordenadas);

    private sealed record PropriedadesPhoton(
        [property: JsonPropertyName("name")] string? Nome,
        [property: JsonPropertyName("street")] string? Rua,
        [property: JsonPropertyName("housenumber")] string? Numero,
        [property: JsonPropertyName("district")] string? Bairro,
        [property: JsonPropertyName("city")] string? Cidade,
        [property: JsonPropertyName("state")] string? Estado,
        [property: JsonPropertyName("postcode")] string? Cep,
        [property: JsonPropertyName("countrycode")] string? CodigoDoPais,
        [property: JsonPropertyName("osm_key")] string? TipoOsm);
}
