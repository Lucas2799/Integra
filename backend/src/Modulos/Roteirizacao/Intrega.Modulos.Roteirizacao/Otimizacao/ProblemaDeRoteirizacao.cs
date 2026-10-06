namespace Intrega.Modulos.Roteirizacao.Otimizacao;

/// <summary>Parada a visitar. Tempos em segundos contados a partir da saída.</summary>
internal sealed record ParadaDoProblema(
    int IndiceNaMatriz,
    int AtendimentoSegundos = 0,
    long? ChegarAPartirDeSegundos = null,
    long? ChegarAteSegundos = null,
    int Prioridade = 0);

/// <summary>IndiceDeChegada nulo = rota aberta (termina na última entrega).</summary>
internal sealed record VeiculoDoProblema(int IndiceDeSaida, int? IndiceDeChegada);

internal sealed record ProblemaDeRoteirizacao(
    long[,] Tempos,
    IReadOnlyList<ParadaDoProblema> Paradas,
    IReadOnlyList<VeiculoDoProblema> Veiculos,
    int TempoLimiteSegundos = 5,
    int? MaximoDeParadasPorVeiculo = null);

/// <summary>Ordem de visita de cada veículo, como índices em <see cref="ProblemaDeRoteirizacao.Paradas"/>.</summary>
internal sealed record SolucaoDeRoteirizacao(
    IReadOnlyList<IReadOnlyList<int>> Rotas,
    IReadOnlyList<int> Descartadas,
    string Algoritmo);

internal interface IOtimizadorDeRotas
{
    SolucaoDeRoteirizacao Otimizar(ProblemaDeRoteirizacao problema, CancellationToken ct);
}
