namespace Intrega.Modulos.Roteirizacao.Otimizacao;

/// <summary>
/// Alternativa sem biblioteca nativa: vizinho mais próximo + melhoria 2-opt.
/// Com vários veículos, cada um pega por vez a parada livre mais próxima. Ignora janelas de horário.
/// </summary>
internal sealed class OtimizadorHeuristico : IOtimizadorDeRotas
{
    public SolucaoDeRoteirizacao Otimizar(ProblemaDeRoteirizacao p, CancellationToken ct)
    {
        var todas = Enumerable.Range(0, p.Paradas.Count).ToList();
        if (p.Veiculos.Count == 1)
        {
            var ordem = VizinhoMaisProximo(p, p.Veiculos[0].IndiceDeSaida, todas);
            Melhorar2Opt(p, p.Veiculos[0], ordem, ct);
            return new SolucaoDeRoteirizacao([ordem], [], "heuristica");
        }

        var grupos = DividirPorVez(p, todas);
        for (var v = 0; v < p.Veiculos.Count; v++) Melhorar2Opt(p, p.Veiculos[v], grupos[v], ct);
        var atribuidas = grupos.SelectMany(g => g).ToHashSet();
        return new SolucaoDeRoteirizacao(grupos.Cast<IReadOnlyList<int>>().ToList(),
            todas.Where(i => !atribuidas.Contains(i)).ToList(), "heuristica");
    }

    private static List<int> VizinhoMaisProximo(ProblemaDeRoteirizacao p, int saida, List<int> paradas)
    {
        var restantes = new HashSet<int>(paradas);
        var ordem = new List<int>(paradas.Count);
        var atual = saida;
        while (restantes.Count > 0)
        {
            // Prioridade alta "encurta" a distância percebida em 1 minuto por nível.
            var proxima = restantes.MinBy(s => p.Tempos[atual, p.Paradas[s].IndiceNaMatriz] - p.Paradas[s].Prioridade * 60L);
            ordem.Add(proxima);
            restantes.Remove(proxima);
            atual = p.Paradas[proxima].IndiceNaMatriz;
        }
        return ordem;
    }

    /// <summary>Cada veículo, na sua vez, pega a parada livre mais próxima de onde está.</summary>
    private static List<List<int>> DividirPorVez(ProblemaDeRoteirizacao p, List<int> paradas)
    {
        var restantes = new HashSet<int>(paradas);
        var grupos = p.Veiculos.Select(_ => new List<int>()).ToList();
        var posicoes = p.Veiculos.Select(v => v.IndiceDeSaida).ToArray();
        while (restantes.Count > 0)
        {
            var avancou = false;
            for (var v = 0; v < p.Veiculos.Count && restantes.Count > 0; v++)
            {
                if (p.MaximoDeParadasPorVeiculo is { } maximo && grupos[v].Count >= maximo) continue;
                var de = posicoes[v];
                var proxima = restantes.MinBy(s => p.Tempos[de, p.Paradas[s].IndiceNaMatriz]);
                grupos[v].Add(proxima);
                restantes.Remove(proxima);
                posicoes[v] = p.Paradas[proxima].IndiceNaMatriz;
                avancou = true;
            }
            if (!avancou) break;
        }
        return grupos;
    }

    internal static long Custo(ProblemaDeRoteirizacao p, VeiculoDoProblema v, IReadOnlyList<int> ordem)
    {
        long custo = 0;
        var atual = v.IndiceDeSaida;
        foreach (var s in ordem)
        {
            custo += p.Tempos[atual, p.Paradas[s].IndiceNaMatriz];
            atual = p.Paradas[s].IndiceNaMatriz;
        }
        if (v.IndiceDeChegada is { } chegada) custo += p.Tempos[atual, chegada];
        return custo;
    }

    /// <summary>2-opt: inverte trechos da rota enquanto isso diminuir o tempo total (desfaz "cruzamentos").</summary>
    private static void Melhorar2Opt(ProblemaDeRoteirizacao p, VeiculoDoProblema v, List<int> ordem, CancellationToken ct)
    {
        if (ordem.Count < 3) return;
        var melhor = Custo(p, v, ordem);
        var melhorou = true;
        var rodadas = 0;
        while (melhorou && rodadas++ < 200)
        {
            ct.ThrowIfCancellationRequested();
            melhorou = false;
            for (var i = 0; i < ordem.Count - 1; i++)
            for (var k = i + 1; k < ordem.Count; k++)
            {
                ordem.Reverse(i, k - i + 1);
                var custo = Custo(p, v, ordem);
                if (custo < melhor)
                {
                    melhor = custo;
                    melhorou = true;
                }
                else
                {
                    ordem.Reverse(i, k - i + 1);
                }
            }
        }
    }
}
