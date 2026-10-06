using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Intrega.Modulos.Geocodificacao.Contratos;
using Intrega.Nucleo.Geo;

namespace Intrega.Modulos.Geocodificacao.Infraestrutura.Provedores;

/// <summary>BrasilAPI CEP v2: gratuita e muitas vezes já devolve as coordenadas.</summary>
internal sealed class ProvedorDeCepBrasilApi(HttpClient http) : IProvedorDeCep
{
    public string Nome => "brasilapi";

    public async Task<InformacoesDoCep?> ConsultarAsync(string cep, CancellationToken ct)
    {
        using var resposta = await http.GetAsync($"api/cep/v2/{cep}", ct);
        if (!resposta.IsSuccessStatusCode) return null;

        var corpo = await resposta.Content.ReadFromJsonAsync<RespostaBrasilApi>(ct);
        if (corpo?.Cidade is null || corpo.Uf is null) return null;

        PontoGeo? local = null;
        if (corpo.Localizacao?.Coordenadas is { } c && LerNumero(c.Latitude, out var lat) && LerNumero(c.Longitude, out var lng))
        {
            var ponto = new PontoGeo(lat, lng);
            if (ponto.Valido) local = ponto;
        }

        return new InformacoesDoCep(cep, Vazio(corpo.Logradouro), Vazio(corpo.Bairro), corpo.Cidade, corpo.Uf, local);
    }

    private static string? Vazio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    // A BrasilAPI às vezes manda as coordenadas como texto, às vezes como número.
    private static bool LerNumero(JsonElement? e, out double valor)
    {
        valor = 0;
        return e switch
        {
            { ValueKind: JsonValueKind.Number } n => n.TryGetDouble(out valor),
            { ValueKind: JsonValueKind.String } s => double.TryParse(s.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out valor),
            _ => false
        };
    }

    private sealed record RespostaBrasilApi(
        [property: JsonPropertyName("state")] string? Uf,
        [property: JsonPropertyName("city")] string? Cidade,
        [property: JsonPropertyName("neighborhood")] string? Bairro,
        [property: JsonPropertyName("street")] string? Logradouro,
        [property: JsonPropertyName("location")] LocalizacaoBrasilApi? Localizacao);

    private sealed record LocalizacaoBrasilApi([property: JsonPropertyName("coordinates")] CoordenadasBrasilApi? Coordenadas);

    private sealed record CoordenadasBrasilApi(
        [property: JsonPropertyName("latitude")] JsonElement? Latitude,
        [property: JsonPropertyName("longitude")] JsonElement? Longitude);
}
