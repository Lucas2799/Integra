using Intrega.Modulos.Paradas.Contratos;
using Intrega.Modulos.Roteirizacao.Dominio;
using Intrega.Modulos.Roteirizacao.Motor;
using Intrega.Modulos.Roteirizacao.Otimizacao;
using Intrega.Nucleo.Geo;
using Intrega.Nucleo.Tempo;

namespace Intrega.Modulos.Roteirizacao.Aplicacao;

/// <summary>
/// Planejador de rotas: monta a matriz de tempos, otimiza a ordem das paradas e calcula previsões de chegada.
/// Também faz o recálculo (destinatário ausente) a partir da posição atual do entregador.
/// </summary>
internal sealed class PlanejadorDeRotas(IMotorDeRotas motor, IOtimizadorDeRotas otimizador, IModuloParadas paradas, TimeProvider relogio)
{
    public sealed record ResultadoDoPlano(int Planejadas, int SemLocalizacao, int ForaDaJanela);

    public async Task<ResultadoDoPlano> OtimizarAsync(Rota rota, PontoGeo? posicaoAtual, CancellationToken ct)
    {
        var todas = await paradas.ListarParaPlanejamentoAsync(rota.Id, ct);
        var pendentes = todas.Where(p => p.Status == StatusDaParada.Pendente && p.Local is not null).ToList();
        var semLocalizacao = todas.Count(p => p.Status == StatusDaParada.Pendente && p.Local is null);

        var horarioDeSaida = HorarioDeSaida(rota, posicaoAtual is not null);
        var saida = posicaoAtual ?? rota.Saida;
        var chegada = rota.Chegada;

        if (pendentes.Count == 0)
        {
            rota.AplicarPlano([], 0, 0, string.Empty, horarioDeSaida, "nenhum", "nenhum", relogio.GetUtcNow());
            return new ResultadoDoPlano(0, semLocalizacao, 0);
        }

        // Ausentes com "tentar no fim da rota" ficam depois de todas as outras paradas.
        var paraOFim = pendentes.Where(VaiParaOFim).ToList();
        var normais = pendentes.Except(paraOFim).ToList();

        // Pontos da matriz: [saída, (chegada), normais..., paraOFim...]
        var pontos = new List<PontoGeo> { saida };
        int? indiceDeChegada = null;
        if (chegada is { } c)
        {
            indiceDeChegada = pontos.Count;
            pontos.Add(c);
        }
        var deslocamento = pontos.Count;
        pontos.AddRange(normais.Select(p => p.Local!.Value));
        pontos.AddRange(paraOFim.Select(p => p.Local!.Value));

        var matriz = await motor.ObterMatrizAsync(pontos, rota.Veiculo, ct);

        // Fase 1: paradas normais (com janelas e "tentar após X min").
        var problemaNormal = normais.Select((p, i) => ParaProblema(p, deslocamento + i, rota, horarioDeSaida)).ToList();
        var fase1 = otimizador.Otimizar(new ProblemaDeRoteirizacao(matriz.Tempos, problemaNormal,
            [new VeiculoDoProblema(0, paraOFim.Count > 0 ? null : indiceDeChegada)]), ct);
        var ordenadas = fase1.Rotas[0].Select(i => normais[i]).ToList();
        ordenadas.AddRange(fase1.Descartadas.Select(i => normais[i]));
        var foraDaJanela = fase1.Descartadas.Count;

        // Fase 2: as de "fim da rota", partindo da última parada normal.
        if (paraOFim.Count > 0)
        {
            var ultimoIndice = ordenadas.Count > 0 ? deslocamento + normais.IndexOf(ordenadas[^1]) : 0;
            var problemaFim = paraOFim.Select((p, i) => ParaProblema(p, deslocamento + normais.Count + i, rota, horarioDeSaida)).ToList();
            var fase2 = otimizador.Otimizar(new ProblemaDeRoteirizacao(matriz.Tempos, problemaFim,
                [new VeiculoDoProblema(ultimoIndice, indiceDeChegada)]), ct);
            ordenadas.AddRange(fase2.Rotas[0].Select(i => paraOFim[i]));
            ordenadas.AddRange(fase2.Descartadas.Select(i => paraOFim[i]));
            foraDaJanela += fase2.Descartadas.Count;
        }

        await AplicarOrdemAsync(rota, ordenadas, saida, horarioDeSaida, fase1.Algoritmo, ct);
        return new ResultadoDoPlano(ordenadas.Count, semLocalizacao, foraDaJanela);
    }

    /// <summary>Calcula trechos, previsões de chegada e traçado para uma ordem já definida (otimizada, manual ou despacho).</summary>
    public async Task AplicarOrdemAsync(Rota rota, IReadOnlyList<ParadaParaPlanejamento> ordenadas, PontoGeo saida,
        DateTimeOffset horarioDeSaida, string algoritmo, CancellationToken ct)
    {
        var localizadas = ordenadas.Where(p => p.Local is not null).ToList();
        var pontos = new List<PontoGeo> { saida };
        pontos.AddRange(localizadas.Select(p => p.Local!.Value));
        if (rota.Chegada is { } chegada && localizadas.Count > 0) pontos.Add(chegada);

        var trajeto = await motor.ObterTrajetoAsync(pontos, rota.Veiculo, comPassos: false, ct);

        var itens = new List<ItemDaSequencia>(localizadas.Count);
        double decorrido = 0;
        for (var i = 0; i < localizadas.Count; i++)
        {
            var parada = localizadas[i];
            var trecho = i < trajeto.Trechos.Count ? trajeto.Trechos[i] : new TrechoDoTrajeto(0, 0, null, []);
            decorrido += trecho.DuracaoSegundos;

            var (aPartirDe, ate) = Janela(parada, rota, horarioDeSaida);
            if (aPartirDe is { } minimo && decorrido < minimo) decorrido = minimo; // espera abrir a janela

            itens.Add(new ItemDaSequencia
            {
                ParadaId = parada.Id,
                Ordem = i + 1,
                DistanciaDoTrechoMetros = trecho.DistanciaMetros,
                DuracaoDoTrechoSegundos = trecho.DuracaoSegundos,
                ChegadaAposSaidaSegundos = decorrido,
                PrevisaoDeChegada = horarioDeSaida.AddSeconds(decorrido),
                AtendimentoSegundos = parada.TempoDeAtendimentoSegundos,
                AtrasadaParaJanela = ate is { } maximo && decorrido > maximo
            });
            decorrido += parada.TempoDeAtendimentoSegundos;
        }

        if (trajeto.Trechos.Count > localizadas.Count) decorrido += trajeto.Trechos[^1].DuracaoSegundos;

        rota.AplicarPlano(itens, trajeto.DistanciaMetros, decorrido, trajeto.Geometria, horarioDeSaida, algoritmo,
            trajeto.Motor, relogio.GetUtcNow());
    }

    /// <summary>Em andamento ou com posição atual: agora. Senão, o horário planejado ou 08:00 do dia da rota.</summary>
    public DateTimeOffset HorarioDeSaida(Rota rota, bool aPartirDaPosicaoAtual)
    {
        var agora = relogio.GetUtcNow();
        if (aPartirDaPosicaoAtual || rota.Status == Contratos.StatusDaRota.EmAndamento) return agora;
        if (rota.HorarioPlanejadoDeSaida is { } planejado) return planejado;
        return rota.Data <= HorarioDeBrasilia.Hoje(relogio) ? agora : HorarioDeBrasilia.Em(rota.Data, new TimeOnly(8, 0));
    }

    private static bool VaiParaOFim(ParadaParaPlanejamento p) =>
        p.Tentativas > 0 && p.EstrategiaDeNovaTentativa == EstrategiaDeNovaTentativa.FimDaRota;

    private static ParadaDoProblema ParaProblema(ParadaParaPlanejamento p, int indiceNaMatriz, Rota rota, DateTimeOffset horarioDeSaida)
    {
        var (aPartirDe, ate) = Janela(p, rota, horarioDeSaida);
        return new ParadaDoProblema(indiceNaMatriz, p.TempoDeAtendimentoSegundos,
            aPartirDe is { } a ? (long)a : null, ate is { } b ? (long)b : null, p.Prioridade);
    }

    /// <summary>Janela da parada em segundos após a saída. "Tentar após X min" vira o início da janela.</summary>
    internal static (double? APartirDe, double? Ate) Janela(ParadaParaPlanejamento p, Rota rota, DateTimeOffset horarioDeSaida)
    {
        double? aPartirDe = p.JanelaInicio is { } inicio ? (HorarioDeBrasilia.Em(rota.Data, inicio) - horarioDeSaida).TotalSeconds : null;
        double? ate = p.JanelaFim is { } fim ? (HorarioDeBrasilia.Em(rota.Data, fim) - horarioDeSaida).TotalSeconds : null;
        if (p.NovaTentativaApos is { } novaTentativa)
            aPartirDe = Math.Max(aPartirDe ?? 0, (novaTentativa - horarioDeSaida).TotalSeconds);
        if (aPartirDe is <= 0) aPartirDe = null;
        return (aPartirDe, ate);
    }
}
