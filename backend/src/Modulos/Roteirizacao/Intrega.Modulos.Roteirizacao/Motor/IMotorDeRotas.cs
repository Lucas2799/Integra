using Intrega.Modulos.Roteirizacao.Contratos;
using Intrega.Nucleo.Geo;

namespace Intrega.Modulos.Roteirizacao.Motor;

/// <summary>Tempo (segundos) e distância (metros) entre todos os pares de pontos.</summary>
internal sealed record MatrizDeDeslocamento(long[,] Tempos, long[,] Distancias)
{
    public int Tamanho => Tempos.GetLength(0);
}

/// <summary>Uma manobra da navegação curva a curva ("Vire à direita na Rua X").</summary>
internal sealed record PassoDeNavegacao(
    string Instrucao,
    string Manobra,
    string? Modificador,
    string? NomeDaVia,
    double DistanciaMetros,
    double DuracaoSegundos,
    PontoGeo Local,
    int? Saida);

internal sealed record TrechoDoTrajeto(double DistanciaMetros, double DuracaoSegundos, string? Geometria, IReadOnlyList<PassoDeNavegacao> Passos);

internal sealed record Trajeto(IReadOnlyList<TrechoDoTrajeto> Trechos, string Geometria, string Motor)
{
    public double DistanciaMetros => Trechos.Sum(t => t.DistanciaMetros);
    public double DuracaoSegundos => Trechos.Sum(t => t.DuracaoSegundos);
}

/// <summary>
/// Motor de rotas. Implementações: OSRM (padrão, gratuito) e linha reta (alternativa).
/// Para usar Google Routes/HERE/Mapbox no futuro, basta implementar esta interface.
/// </summary>
internal interface IMotorDeRotas
{
    Task<MatrizDeDeslocamento> ObterMatrizAsync(IReadOnlyList<PontoGeo> pontos, PerfilDeVeiculo perfil, CancellationToken ct);

    Task<Trajeto> ObterTrajetoAsync(IReadOnlyList<PontoGeo> pontos, PerfilDeVeiculo perfil, bool comPassos, CancellationToken ct);
}
