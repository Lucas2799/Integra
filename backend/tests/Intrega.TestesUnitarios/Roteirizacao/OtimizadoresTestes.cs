using Intrega.Modulos.Roteirizacao.Otimizacao;

namespace Intrega.TestesUnitarios.Roteirizacao;

public class OtimizadoresTestes
{
    /// <summary>
    /// Pontos numa linha reta: 0 (saída) — 1 — 2 — 3 — 4. Tempo = distância entre índices × 60 s.
    /// A melhor ordem saindo de 0 é obviamente 1, 2, 3, 4.
    /// </summary>
    private static long[,] MatrizEmLinha(int n)
    {
        var m = new long[n, n];
        for (var i = 0; i < n; i++)
        for (var j = 0; j < n; j++)
            m[i, j] = Math.Abs(i - j) * 60L;
        return m;
    }

    // Paradas fornecidas fora de ordem de propósito.
    private static readonly List<ParadaDoProblema> ParadasEmbaralhadas =
        [new(3), new(1), new(4), new(2)];

    public static TheoryData<string> Otimizadores => ["ortools", "heuristica"];

    private static IOtimizadorDeRotas Criar(string nome) =>
        nome == "ortools" ? new OtimizadorOrTools() : new OtimizadorHeuristico();

    [Theory]
    [MemberData(nameof(Otimizadores))]
    public void Encontra_a_ordem_otima_numa_linha(string nome)
    {
        var problema = new ProblemaDeRoteirizacao(MatrizEmLinha(5), ParadasEmbaralhadas, [new VeiculoDoProblema(0, null)]);

        var solucao = Criar(nome).Otimizar(problema, CancellationToken.None);

        var indicesNaMatriz = solucao.Rotas[0].Select(i => ParadasEmbaralhadas[i].IndiceNaMatriz).ToList();
        Assert.Equal([1, 2, 3, 4], indicesNaMatriz);
        Assert.Empty(solucao.Descartadas);
    }

    [Theory]
    [MemberData(nameof(Otimizadores))]
    public void Com_varios_entregadores_todas_as_paradas_sao_atribuidas_uma_unica_vez(string nome)
    {
        var paradas = Enumerable.Range(1, 9).Select(i => new ParadaDoProblema(i)).ToList();
        var problema = new ProblemaDeRoteirizacao(MatrizEmLinha(10), paradas,
            [new VeiculoDoProblema(0, null), new VeiculoDoProblema(0, null), new VeiculoDoProblema(0, null)],
            MaximoDeParadasPorVeiculo: 4);

        var solucao = Criar(nome).Otimizar(problema, CancellationToken.None);

        var todas = solucao.Rotas.SelectMany(r => r).Concat(solucao.Descartadas).ToList();
        Assert.Equal(9, todas.Count);
        Assert.Equal(9, todas.Distinct().Count());
        Assert.All(solucao.Rotas, r => Assert.True(r.Count <= 4));
    }

    /// <summary>Simula a rota devolvida e retorna o horário de chegada (s) em cada parada.</summary>
    private static Dictionary<int, long> Chegadas(ProblemaDeRoteirizacao p, IReadOnlyList<int> ordem)
    {
        var chegadas = new Dictionary<int, long>();
        long tempo = 0;
        var atual = p.Veiculos[0].IndiceDeSaida;
        foreach (var i in ordem)
        {
            var parada = p.Paradas[i];
            tempo += p.Tempos[atual, parada.IndiceNaMatriz];
            tempo = Math.Max(tempo, parada.ChegarAPartirDeSegundos ?? 0);
            chegadas[i] = tempo;
            tempo += parada.AtendimentoSegundos;
            atual = parada.IndiceNaMatriz;
        }
        return chegadas;
    }

    [Fact]
    public void OrTools_chega_antes_do_fim_da_janela()
    {
        // Com 1 min de atendimento, seguir a linha (1,2,3,4) chegaria na parada 4 aos 7 min.
        // Ela precisa ser atendida até 5 min: o otimizador tem que achar outra ordem.
        var paradas = new List<ParadaDoProblema>
        {
            new(1, 60), new(2, 60), new(3, 60), new(4, 60, ChegarAteSegundos: 300)
        };
        var problema = new ProblemaDeRoteirizacao(MatrizEmLinha(5), paradas, [new VeiculoDoProblema(0, null)]);

        var solucao = new OtimizadorOrTools().Otimizar(problema, CancellationToken.None);

        Assert.Equal(4, solucao.Rotas[0].Count);
        Assert.True(Chegadas(problema, solucao.Rotas[0])[3] <= 300);
    }

    [Fact]
    public void OrTools_nunca_atende_antes_do_inicio_da_janela()
    {
        // A parada 1 só abre aos 10 min. Seja qual for a ordem, a chegada nela não pode ser antes disso
        // e nenhuma parada pode ser descartada.
        var paradas = new List<ParadaDoProblema>
        {
            new(1, ChegarAPartirDeSegundos: 600), new(2), new(3), new(4)
        };
        var problema = new ProblemaDeRoteirizacao(MatrizEmLinha(5), paradas, [new VeiculoDoProblema(0, null)]);

        var solucao = new OtimizadorOrTools().Otimizar(problema, CancellationToken.None);

        Assert.Empty(solucao.Descartadas);
        Assert.True(Chegadas(problema, solucao.Rotas[0])[0] >= 600);
    }

    [Fact]
    public void OrTools_descarta_em_vez_de_falhar_quando_e_impossivel()
    {
        // Parada a 4 min de distância que precisaria ser atendida em 1 min: inviável, mas o solver não pode quebrar.
        var paradas = new List<ParadaDoProblema> { new(4, ChegarAteSegundos: 60), new(1) };
        var problema = new ProblemaDeRoteirizacao(MatrizEmLinha(5), paradas, [new VeiculoDoProblema(0, null)]);

        var solucao = new OtimizadorOrTools().Otimizar(problema, CancellationToken.None);

        Assert.Equal(2, solucao.Rotas[0].Count + solucao.Descartadas.Count);
    }

    [Fact]
    public void Rota_que_volta_ao_inicio_considera_o_retorno()
    {
        var paradas = new List<ParadaDoProblema> { new(1), new(2) };
        var veiculo = new VeiculoDoProblema(0, 0);
        var problema = new ProblemaDeRoteirizacao(MatrizEmLinha(3), paradas, [veiculo]);

        var solucao = new OtimizadorOrTools().Otimizar(problema, CancellationToken.None);

        Assert.Equal(240, OtimizadorHeuristico.Custo(problema, veiculo, solucao.Rotas[0]));
    }
}
