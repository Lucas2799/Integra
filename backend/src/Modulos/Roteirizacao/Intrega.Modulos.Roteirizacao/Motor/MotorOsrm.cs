using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Intrega.Modulos.Roteirizacao.Contratos;
using Intrega.Nucleo.Geo;
using Microsoft.Extensions.Options;

namespace Intrega.Modulos.Roteirizacao.Motor;

/// <summary>Cliente do OSRM (Open Source Routing Machine). Divide matrizes e trajetos grandes em blocos.</summary>
internal sealed class MotorOsrm(HttpClient http, IOptions<OpcoesDeRoteirizacao> opcoes) : IMotorDeRotas
{
    private OpcoesOsrm Osrm => opcoes.Value.Osrm;

    public string? UrlDoPerfil(PerfilDeVeiculo perfil) => perfil switch
    {
        PerfilDeVeiculo.Moto => Vazio(Osrm.UrlMoto) ?? Vazio(Osrm.UrlCarro),
        PerfilDeVeiculo.Bicicleta => Vazio(Osrm.UrlBicicleta),
        PerfilDeVeiculo.APe => Vazio(Osrm.UrlAPe),
        _ => Vazio(Osrm.UrlCarro)
    };

    private double FatorDeTempo(PerfilDeVeiculo perfil) =>
        perfil == PerfilDeVeiculo.Moto && string.IsNullOrEmpty(Osrm.UrlMoto) ? Osrm.FatorDeTempoMoto : 1.0;

    public async Task<MatrizDeDeslocamento> ObterMatrizAsync(IReadOnlyList<PontoGeo> pontos, PerfilDeVeiculo perfil, CancellationToken ct)
    {
        var url = UrlDoPerfil(perfil) ?? throw new InvalidOperationException($"OSRM não configurado para {perfil}.");
        var n = pontos.Count;
        var tempos = new long[n, n];
        var distancias = new long[n, n];
        var coordenadas = Coordenadas(pontos);
        var bloco = Math.Max(1, Osrm.TamanhoMaximoDaMatriz);
        var fator = FatorDeTempo(perfil);

        // Matriz grande vira vários pedidos de no máximo bloco × bloco.
        for (var o = 0; o < n; o += bloco)
        for (var d = 0; d < n; d += bloco)
        {
            var origens = Enumerable.Range(o, Math.Min(bloco, n - o)).ToArray();
            var destinos = Enumerable.Range(d, Math.Min(bloco, n - d)).ToArray();
            var endereco = $"{url.TrimEnd('/')}/table/v1/driving/{coordenadas}?annotations=duration,distance" +
                           $"&sources={string.Join(';', origens)}&destinations={string.Join(';', destinos)}";

            var tabela = await http.GetFromJsonAsync<TabelaOsrm>(endereco, ct)
                         ?? throw new HttpRequestException("Resposta vazia do OSRM.");
            if (tabela.Codigo != "Ok" || tabela.Duracoes is null)
                throw new HttpRequestException($"OSRM /table falhou: {tabela.Codigo}");

            for (var i = 0; i < origens.Length; i++)
            for (var j = 0; j < destinos.Length; j++)
            {
                var (oi, dj) = (origens[i], destinos[j]);
                var duracao = tabela.Duracoes[i][j];
                var distancia = tabela.Distancias?[i][j];
                if (duracao is null || distancia is null)
                {
                    // Ponto fora da malha viária (ex.: pino dentro de um condomínio): estima em linha reta.
                    (tempos[oi, dj], distancias[oi, dj]) = MotorLinhaReta.Estimar(pontos[oi], pontos[dj], perfil);
                    continue;
                }
                tempos[oi, dj] = (long)Math.Round(duracao.Value * fator);
                distancias[oi, dj] = (long)Math.Round(distancia.Value);
            }
        }
        return new MatrizDeDeslocamento(tempos, distancias);
    }

    public async Task<Trajeto> ObterTrajetoAsync(IReadOnlyList<PontoGeo> pontos, PerfilDeVeiculo perfil, bool comPassos, CancellationToken ct)
    {
        var url = UrlDoPerfil(perfil) ?? throw new InvalidOperationException($"OSRM não configurado para {perfil}.");
        var fator = FatorDeTempo(perfil);
        var trechos = new List<TrechoDoTrajeto>();
        var geometria = new List<PontoGeo>();
        var tamanho = Math.Max(2, Osrm.MaximoDePontosPorTrajeto);

        // Blocos com 1 ponto em comum: [0..89], [89..178], ...
        for (var inicio = 0; inicio < pontos.Count - 1; inicio += tamanho - 1)
        {
            var parte = pontos.Skip(inicio).Take(tamanho).ToList();
            var endereco = $"{url.TrimEnd('/')}/route/v1/driving/{Coordenadas(parte)}" +
                           $"?overview=full&geometries=polyline&steps={(comPassos ? "true" : "false")}";
            var resposta = await http.GetFromJsonAsync<RespostaRotaOsrm>(endereco, ct)
                           ?? throw new HttpRequestException("Resposta vazia do OSRM.");
            if (resposta.Codigo != "Ok" || resposta.Rotas is not { Count: > 0 })
                throw new HttpRequestException($"OSRM /route falhou: {resposta.Codigo}");

            var rota = resposta.Rotas[0];
            var decodificada = Polilinha.Decodificar(rota.Geometria ?? "");
            geometria.AddRange(geometria.Count > 0 ? decodificada.Skip(1) : decodificada);

            foreach (var trecho in rota.Trechos)
            {
                var passos = comPassos ? trecho.Passos?.Select(p => ParaPasso(p, fator)).ToList() ?? [] : [];
                string? geometriaDoTrecho = null;
                if (comPassos && trecho.Passos is { Count: > 0 })
                {
                    var pts = new List<PontoGeo>();
                    foreach (var passo in trecho.Passos)
                    {
                        var d = Polilinha.Decodificar(passo.Geometria ?? "");
                        pts.AddRange(pts.Count > 0 ? d.Skip(1) : d);
                    }
                    geometriaDoTrecho = Polilinha.Codificar(pts);
                }
                trechos.Add(new TrechoDoTrajeto(trecho.Distancia, trecho.Duracao * fator, geometriaDoTrecho, passos));
            }
        }
        return new Trajeto(trechos, Polilinha.Codificar(geometria), "osrm");
    }

    private static PassoDeNavegacao ParaPasso(PassoOsrm p, double fator)
    {
        var m = p.Manobra;
        var local = m?.Local is { Length: 2 } l ? new PontoGeo(l[1], l[0]) : default;
        var via = string.IsNullOrWhiteSpace(p.Nome) ? p.Referencia : p.Nome;
        var tipo = m?.Tipo ?? "continue";
        return new PassoDeNavegacao(
            GeradorDeInstrucoes.Gerar(tipo, m?.Modificador, via, m?.Saida),
            tipo, m?.Modificador, via, p.Distancia, p.Duracao * fator, local, m?.Saida);
    }

    /// <summary>O OSRM recebe "longitude,latitude;longitude,latitude;...".</summary>
    private static string Coordenadas(IEnumerable<PontoGeo> pontos)
    {
        var sb = new StringBuilder();
        foreach (var p in pontos)
        {
            if (sb.Length > 0) sb.Append(';');
            sb.Append(p.Longitude.ToString("F6", CultureInfo.InvariantCulture)).Append(',')
              .Append(p.Latitude.ToString("F6", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    private static string? Vazio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private sealed record TabelaOsrm(
        [property: JsonPropertyName("code")] string Codigo,
        [property: JsonPropertyName("durations")] double?[][]? Duracoes,
        [property: JsonPropertyName("distances")] double?[][]? Distancias);

    private sealed record RespostaRotaOsrm(
        [property: JsonPropertyName("code")] string Codigo,
        [property: JsonPropertyName("routes")] List<RotaOsrm>? Rotas);

    private sealed record RotaOsrm(
        [property: JsonPropertyName("geometry")] string? Geometria,
        [property: JsonPropertyName("legs")] List<TrechoOsrm> Trechos);

    private sealed record TrechoOsrm(
        [property: JsonPropertyName("distance")] double Distancia,
        [property: JsonPropertyName("duration")] double Duracao,
        [property: JsonPropertyName("steps")] List<PassoOsrm>? Passos);

    private sealed record PassoOsrm(
        [property: JsonPropertyName("distance")] double Distancia,
        [property: JsonPropertyName("duration")] double Duracao,
        [property: JsonPropertyName("name")] string? Nome,
        [property: JsonPropertyName("ref")] string? Referencia,
        [property: JsonPropertyName("geometry")] string? Geometria,
        [property: JsonPropertyName("maneuver")] ManobraOsrm? Manobra);

    private sealed record ManobraOsrm(
        [property: JsonPropertyName("type")] string? Tipo,
        [property: JsonPropertyName("modifier")] string? Modificador,
        [property: JsonPropertyName("location")] double[]? Local,
        [property: JsonPropertyName("exit")] int? Saida);
}
