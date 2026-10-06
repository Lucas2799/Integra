using Intrega.Modulos.Roteirizacao.Contratos;
using Intrega.Nucleo.Geo;

namespace Intrega.Modulos.Roteirizacao.Motor;

/// <summary>Estimativa por linha reta × fator de desvio. Usada quando o OSRM está fora do ar.</summary>
internal sealed class MotorLinhaReta : IMotorDeRotas
{
    /// <summary>As ruas raramente são retas: a distância real é ~35% maior que a linha reta.</summary>
    public const double FatorDeDesvio = 1.35;

    public static double VelocidadeEmMetrosPorSegundo(PerfilDeVeiculo perfil) => perfil switch
    {
        PerfilDeVeiculo.Moto => 35 / 3.6,
        PerfilDeVeiculo.Bicicleta => 15 / 3.6,
        PerfilDeVeiculo.APe => 5 / 3.6,
        _ => 28 / 3.6
    };

    public static (long Tempo, long Distancia) Estimar(PontoGeo a, PontoGeo b, PerfilDeVeiculo perfil)
    {
        var distancia = a.DistanciaAte(b) * FatorDeDesvio;
        return ((long)Math.Round(distancia / VelocidadeEmMetrosPorSegundo(perfil)), (long)Math.Round(distancia));
    }

    public Task<MatrizDeDeslocamento> ObterMatrizAsync(IReadOnlyList<PontoGeo> pontos, PerfilDeVeiculo perfil, CancellationToken ct)
    {
        var n = pontos.Count;
        var tempos = new long[n, n];
        var distancias = new long[n, n];
        for (var i = 0; i < n; i++)
        for (var j = 0; j < n; j++)
        {
            if (i == j) continue;
            (tempos[i, j], distancias[i, j]) = Estimar(pontos[i], pontos[j], perfil);
        }
        return Task.FromResult(new MatrizDeDeslocamento(tempos, distancias));
    }

    public Task<Trajeto> ObterTrajetoAsync(IReadOnlyList<PontoGeo> pontos, PerfilDeVeiculo perfil, bool comPassos, CancellationToken ct)
    {
        var trechos = new List<TrechoDoTrajeto>();
        for (var i = 0; i < pontos.Count - 1; i++)
        {
            var (tempo, distancia) = Estimar(pontos[i], pontos[i + 1], perfil);
            var passos = comPassos
                ? new List<PassoDeNavegacao>
                {
                    new("Siga em direção ao destino", "depart", null, null, distancia, tempo, pontos[i], null),
                    new("Você chegou ao destino", "arrive", null, null, 0, 0, pontos[i + 1], null)
                }
                : [];
            trechos.Add(new TrechoDoTrajeto(distancia, tempo, Polilinha.Codificar([pontos[i], pontos[i + 1]]), passos));
        }
        return Task.FromResult(new Trajeto(trechos, Polilinha.Codificar(pontos), "linha_reta"));
    }
}
