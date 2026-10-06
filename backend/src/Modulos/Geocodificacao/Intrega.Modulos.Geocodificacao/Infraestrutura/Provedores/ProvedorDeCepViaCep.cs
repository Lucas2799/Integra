using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Intrega.Modulos.Geocodificacao.Contratos;

namespace Intrega.Modulos.Geocodificacao.Infraestrutura.Provedores;

/// <summary>ViaCEP: alternativa gratuita quando a BrasilAPI falha (não traz coordenadas).</summary>
internal sealed class ProvedorDeCepViaCep(HttpClient http) : IProvedorDeCep
{
    public string Nome => "viacep";

    public async Task<InformacoesDoCep?> ConsultarAsync(string cep, CancellationToken ct)
    {
        using var resposta = await http.GetAsync($"ws/{cep}/json/", ct);
        if (!resposta.IsSuccessStatusCode) return null;
        var corpo = await resposta.Content.ReadFromJsonAsync<RespostaViaCep>(ct);
        if (corpo is null || corpo.Erro is not null || corpo.Localidade is null || corpo.Uf is null) return null;
        return new InformacoesDoCep(cep, Vazio(corpo.Logradouro), Vazio(corpo.Bairro), corpo.Localidade, corpo.Uf, null);
    }

    private static string? Vazio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private sealed record RespostaViaCep(
        [property: JsonPropertyName("logradouro")] string? Logradouro,
        [property: JsonPropertyName("bairro")] string? Bairro,
        [property: JsonPropertyName("localidade")] string? Localidade,
        [property: JsonPropertyName("uf")] string? Uf,
        [property: JsonPropertyName("erro")] object? Erro);
}
