using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Intrega.Modulos.Geocodificacao.Aplicacao;
using Intrega.Modulos.Geocodificacao.Contratos;
using Intrega.Nucleo.Geo;
using Microsoft.Extensions.Options;

namespace Intrega.Modulos.Geocodificacao.Infraestrutura.Provedores;

/// <summary>
/// Nominatim (OpenStreetMap). Na instância pública: no máximo 1 requisição/s e proibido usar para autocompletar.
/// Para escalar sem custo de licença, hospede uma instância própria e ajuste Geocodificacao:Nominatim:UrlBase.
/// </summary>
internal sealed class GeocodificadorNominatim(HttpClient http, LimitadorNominatim limitador)
    : IGeocodificador, IGeocodificadorReverso
{
    private const string NomeDoProvedor = "nominatim";

    public async Task<CandidatoDeGeocodificacao?> GeocodificarPorCamposAsync(EnderecoInformado e, PontoGeo? proximoDe, CancellationToken ct)
    {
        var rua = string.Join(" ", new[] { e.Numero, e.Logradouro }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var parametros = new Dictionary<string, string?>
        {
            ["street"] = rua,
            ["city"] = e.Cidade,
            ["state"] = UfsBrasileiras.NomeDoEstado(e.Uf),
            ["postalcode"] = NormalizadorDeEndereco.NormalizarCep(e.Cep) is { } cep ? $"{cep[..5]}-{cep[5..]}" : null,
            ["country"] = "Brasil"
        };
        var lugares = await PesquisarAsync(parametros, proximoDe, ct);
        return lugares.Count == 0 ? null : ParaCandidato(lugares[0], e.Numero);
    }

    public async Task<CandidatoDeGeocodificacao?> GeocodificarTextoAsync(string texto, PontoGeo? proximoDe, CancellationToken ct)
    {
        var lugares = await PesquisarAsync(new Dictionary<string, string?> { ["q"] = texto }, proximoDe, ct);
        return lugares.Count == 0 ? null : ParaCandidato(lugares[0], null);
    }

    public async Task<EnderecoNormalizado?> ReversoAsync(PontoGeo ponto, CancellationToken ct)
    {
        var url = FormattableString.Invariant(
            $"reverse?format=jsonv2&addressdetails=1&accept-language=pt-BR&lat={ponto.Latitude}&lon={ponto.Longitude}");
        await limitador.AguardarVezAsync(ct);
        using var resposta = await http.GetAsync(url, ct);
        if (!resposta.IsSuccessStatusCode) return null;
        var lugar = await resposta.Content.ReadFromJsonAsync<LugarNominatim>(ct);
        return lugar?.Endereco is null ? null : ParaEndereco(lugar.Endereco, lugar.NomeCompleto);
    }

    private async Task<List<LugarNominatim>> PesquisarAsync(Dictionary<string, string?> parametros, PontoGeo? proximoDe, CancellationToken ct)
    {
        // Prioriza resultados numa caixa de ~50 km em volta da região esperada (sem excluir os de fora).
        if (proximoDe is { } p)
            parametros["viewbox"] = FormattableString.Invariant(
                $"{p.Longitude - 0.45},{p.Latitude + 0.45},{p.Longitude + 0.45},{p.Latitude - 0.45}");

        var consulta = string.Join("&", parametros
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value!)}"));
        var url = $"search?format=jsonv2&addressdetails=1&limit=1&countrycodes=br&accept-language=pt-BR&{consulta}";

        await limitador.AguardarVezAsync(ct);
        using var resposta = await http.GetAsync(url, ct);
        if (!resposta.IsSuccessStatusCode) return [];
        return await resposta.Content.ReadFromJsonAsync<List<LugarNominatim>>(ct) ?? [];
    }

    private static CandidatoDeGeocodificacao? ParaCandidato(LugarNominatim lugar, string? numeroPedido)
    {
        if (!double.TryParse(lugar.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
            !double.TryParse(lugar.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
            return null;

        var endereco = lugar.Endereco is null
            ? new EnderecoNormalizado(null, null, null, null, null, null, null, lugar.NomeCompleto ?? "")
            : ParaEndereco(lugar.Endereco, lugar.NomeCompleto);

        var numeroConfere = lugar.Endereco?.Numero is not null &&
                            (numeroPedido is null ||
                             NormalizadorDeEndereco.Simplificar(lugar.Endereco.Numero) == NormalizadorDeEndereco.Simplificar(numeroPedido));
        var precisao = numeroConfere ? PrecisaoDoResultado.Numero
            : lugar.Endereco?.Rua is not null ? PrecisaoDoResultado.Rua
            : lugar.Endereco?.Bairro is not null ? PrecisaoDoResultado.Bairro
            : PrecisaoDoResultado.Cep;

        return new CandidatoDeGeocodificacao(new PontoGeo(lat, lon), endereco, precisao, NomeDoProvedor);
    }

    private static EnderecoNormalizado ParaEndereco(EnderecoNominatim a, string? nomeCompleto)
    {
        var cidade = a.Cidade ?? a.CidadePequena ?? a.Vila ?? a.Municipio;
        var uf = UfsBrasileiras.ParaUf(a.CodigoDoEstado ?? a.Estado);
        var bairro = a.Bairro ?? a.Vizinhanca ?? a.Distrito;
        var descricao = NormalizadorDeEndereco.MontarDescricao(a.Rua, a.Numero, null, bairro, cidade, uf);
        return new EnderecoNormalizado(a.Rua, a.Numero, null, bairro, cidade, uf,
            NormalizadorDeEndereco.NormalizarCep(a.Cep), string.IsNullOrEmpty(descricao) ? nomeCompleto ?? "" : descricao);
    }

    private sealed record LugarNominatim(
        [property: JsonPropertyName("lat")] string? Lat,
        [property: JsonPropertyName("lon")] string? Lon,
        [property: JsonPropertyName("display_name")] string? NomeCompleto,
        [property: JsonPropertyName("address")] EnderecoNominatim? Endereco);

    private sealed record EnderecoNominatim(
        [property: JsonPropertyName("road")] string? Rua,
        [property: JsonPropertyName("house_number")] string? Numero,
        [property: JsonPropertyName("suburb")] string? Bairro,
        [property: JsonPropertyName("neighbourhood")] string? Vizinhanca,
        [property: JsonPropertyName("city_district")] string? Distrito,
        [property: JsonPropertyName("city")] string? Cidade,
        [property: JsonPropertyName("town")] string? CidadePequena,
        [property: JsonPropertyName("village")] string? Vila,
        [property: JsonPropertyName("municipality")] string? Municipio,
        [property: JsonPropertyName("state")] string? Estado,
        [property: JsonPropertyName("ISO3166-2-lvl4")] string? CodigoDoEstado,
        [property: JsonPropertyName("postcode")] string? Cep);
}

/// <summary>Garante o intervalo mínimo entre chamadas ao Nominatim (uma instância para o app todo).</summary>
internal sealed class LimitadorNominatim(IOptions<OpcoesDeGeocodificacao> opcoes, TimeProvider relogio)
{
    private readonly SemaphoreSlim _trava = new(1, 1);
    private DateTimeOffset _ultimaChamada = DateTimeOffset.MinValue;

    public async Task AguardarVezAsync(CancellationToken ct)
    {
        var intervalo = TimeSpan.FromMilliseconds(opcoes.Value.Nominatim.IntervaloMinimoMs);
        if (intervalo <= TimeSpan.Zero) return;

        await _trava.WaitAsync(ct);
        try
        {
            var espera = _ultimaChamada + intervalo - relogio.GetUtcNow();
            if (espera > TimeSpan.Zero) await Task.Delay(espera, relogio, ct);
            _ultimaChamada = relogio.GetUtcNow();
        }
        finally
        {
            _trava.Release();
        }
    }
}
