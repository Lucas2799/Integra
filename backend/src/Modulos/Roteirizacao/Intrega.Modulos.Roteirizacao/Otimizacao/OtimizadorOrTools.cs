using Google.OrTools.ConstraintSolver;

namespace Intrega.Modulos.Roteirizacao.Otimizacao;

/// <summary>
/// Google OR-Tools (licença Apache 2.0, gratuito): resolve o problema do caixeiro-viajante (1 entregador)
/// e de roteirização de veículos (vários), com janelas de horário, prioridade, rota aberta e equilíbrio de jornada.
/// </summary>
internal sealed class OtimizadorOrTools : IOtimizadorDeRotas
{
    private const long PenalidadeParaDescartar = 10_000_000;
    private const long PenalidadePorSegundoDeAtraso = 100;
    private const long EsperaMaximaSegundos = 4 * 3600;
    private const long HorizonteSegundos = 48 * 3600;

    public SolucaoDeRoteirizacao Otimizar(ProblemaDeRoteirizacao p, CancellationToken ct)
    {
        if (p.Paradas.Count == 0)
            return new SolucaoDeRoteirizacao(p.Veiculos.Select(_ => (IReadOnlyList<int>)[]).ToList(), [], "ortools");

        // Nós do solver: [pontos de saída/chegada distintos] [chegada virtual, se houver rota aberta] [paradas]
        var depositos = p.Veiculos
            .SelectMany(v => v.IndiceDeChegada is { } c ? new[] { v.IndiceDeSaida, c } : [v.IndiceDeSaida])
            .Distinct().ToList();
        var temRotaAberta = p.Veiculos.Any(v => v.IndiceDeChegada is null);
        var chegadaVirtual = temRotaAberta ? depositos.Count : -1;
        var primeiraParada = depositos.Count + (temRotaAberta ? 1 : 0);
        var totalDeNos = primeiraParada + p.Paradas.Count;

        int IndiceNaMatriz(int no) => no < depositos.Count ? depositos[no] : p.Paradas[no - primeiraParada].IndiceNaMatriz;
        // A chegada virtual custa zero: o entregador "termina" onde fizer a última entrega.
        long Deslocamento(int de, int para) =>
            de == chegadaVirtual || para == chegadaVirtual ? 0 : p.Tempos[IndiceNaMatriz(de), IndiceNaMatriz(para)];
        long Atendimento(int no) => no >= primeiraParada ? p.Paradas[no - primeiraParada].AtendimentoSegundos : 0;

        var saidas = p.Veiculos.Select(v => depositos.IndexOf(v.IndiceDeSaida)).ToArray();
        var chegadas = p.Veiculos.Select(v => v.IndiceDeChegada is { } c ? depositos.IndexOf(c) : chegadaVirtual).ToArray();

        using var gerenciador = new RoutingIndexManager(totalDeNos, p.Veiculos.Count, saidas, chegadas);
        using var modelo = new RoutingModel(gerenciador);

        var custo = modelo.RegisterTransitCallback((long de, long para) =>
            Deslocamento(gerenciador.IndexToNode(de), gerenciador.IndexToNode(para)));
        modelo.SetArcCostEvaluatorOfAllVehicles(custo);

        var tempo = modelo.RegisterTransitCallback((long de, long para) =>
        {
            var origem = gerenciador.IndexToNode(de);
            return Deslocamento(origem, gerenciador.IndexToNode(para)) + Atendimento(origem);
        });
        modelo.AddDimension(tempo, EsperaMaximaSegundos, HorizonteSegundos, true, "Tempo");
        var dimensaoTempo = modelo.GetMutableDimension("Tempo");
        // A duração total (inclusive esperando janela abrir) também é custo: entregador parado é tempo perdido.
        dimensaoTempo.SetSpanCostCoefficientForAllVehicles(1);

        for (var i = 0; i < p.Paradas.Count; i++)
        {
            var parada = p.Paradas[i];
            var indice = gerenciador.NodeToIndex(primeiraParada + i);
            if (parada.ChegarAPartirDeSegundos is { } aPartirDe && aPartirDe > 0)
                dimensaoTempo.CumulVar(indice).SetMin(Math.Min(aPartirDe, HorizonteSegundos));
            if (parada.ChegarAteSegundos is { } ate)
                dimensaoTempo.SetCumulVarSoftUpperBound(indice, Math.Max(0, ate), PenalidadePorSegundoDeAtraso);
            else if (parada.Prioridade > 0)
                dimensaoTempo.SetCumulVarSoftUpperBound(indice, 0, parada.Prioridade); // quanto maior, mais cedo

            // Se o problema for impossível (janelas conflitantes), descarta em vez de falhar; o planejador põe no fim.
            modelo.AddDisjunction([indice], PenalidadeParaDescartar);
        }

        if (p.Veiculos.Count > 1)
        {
            // Equilibra a jornada entre os entregadores.
            dimensaoTempo.SetGlobalSpanCostCoefficient(5);
        }

        if (p.MaximoDeParadasPorVeiculo is { } maximo)
        {
            var contagem = modelo.RegisterUnaryTransitCallback(indice =>
                gerenciador.IndexToNode(indice) >= primeiraParada ? 1 : 0);
            modelo.AddDimension(contagem, 0, maximo, true, "Quantidade");
        }

        var parametros = operations_research_constraint_solver.DefaultRoutingSearchParameters();
        parametros.FirstSolutionStrategy = FirstSolutionStrategy.Types.Value.PathCheapestArc;
        parametros.LocalSearchMetaheuristic = LocalSearchMetaheuristic.Types.Value.GuidedLocalSearch;
        parametros.TimeLimit = new Google.Protobuf.WellKnownTypes.Duration { Seconds = TempoLimite(p) };

        ct.ThrowIfCancellationRequested();
        var solucao = modelo.SolveWithParameters(parametros)
                      ?? throw new InvalidOperationException($"OR-Tools não encontrou solução ({modelo.GetStatus()}).");

        var rotas = new List<IReadOnlyList<int>>();
        var visitadas = new HashSet<int>();
        for (var v = 0; v < p.Veiculos.Count; v++)
        {
            var rota = new List<int>();
            var indice = modelo.Start(v);
            while (!modelo.IsEnd(indice))
            {
                var no = gerenciador.IndexToNode(indice);
                if (no >= primeiraParada)
                {
                    rota.Add(no - primeiraParada);
                    visitadas.Add(no - primeiraParada);
                }
                indice = solucao.Value(modelo.NextVar(indice));
            }
            rotas.Add(rota);
        }

        var descartadas = Enumerable.Range(0, p.Paradas.Count).Where(i => !visitadas.Contains(i)).ToList();
        return new SolucaoDeRoteirizacao(rotas, descartadas, "ortools");
    }

    /// <summary>A busca local guiada usa todo o tempo disponível; problemas pequenos não precisam de 5 s.</summary>
    private static long TempoLimite(ProblemaDeRoteirizacao p) => p.Paradas.Count switch
    {
        <= 12 => 1,
        <= 40 => Math.Min(2, p.TempoLimiteSegundos),
        _ => Math.Max(1, p.TempoLimiteSegundos)
    };
}
