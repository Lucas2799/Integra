using Intrega.Modulos.Identidade.Contratos;
using Intrega.Modulos.Paradas.Contratos;
using Intrega.Modulos.Roteirizacao.Contratos;
using Intrega.Modulos.Roteirizacao.Dominio;
using Intrega.Modulos.Roteirizacao.Infraestrutura;
using Intrega.Modulos.Roteirizacao.Motor;
using Intrega.Nucleo.Autenticacao;
using Intrega.Nucleo.Eventos;
using Intrega.Nucleo.Geo;
using Intrega.Nucleo.Resultados;
using Intrega.Nucleo.Tempo;
using Microsoft.EntityFrameworkCore;

namespace Intrega.Modulos.Roteirizacao.Aplicacao;

internal sealed class ServicoDeRotas(
    ContextoRoteirizacao bd,
    PlanejadorDeRotas planejador,
    IMotorDeRotas motor,
    IModuloParadas paradas,
    IModuloIdentidade identidade,
    IUsuarioAtual usuario,
    IBarramentoDeEventos barramento,
    TimeProvider relogio)
{
    private static readonly Erro RotaNaoEncontrada = Erro.NaoEncontrado("rota.nao_encontrada", "Rota não encontrada.");

    public async Task<Resultado<DetalhesDaRotaDto>> CriarAsync(ConfiguracaoDaRotaRequisicao req, CancellationToken ct)
    {
        var data = req.Data ?? HorarioDeBrasilia.Hoje(relogio);
        var rota = Rota.Criar(usuario.Id, usuario.Id, usuario.OrganizacaoId,
            string.IsNullOrWhiteSpace(req.Nome) ? $"Rota {data:dd/MM}" : req.Nome, data, req.Veiculo,
            req.Saida.ParaPonto(), req.Saida.Descricao, req.Chegada?.ParaPonto(), req.Chegada?.Descricao,
            req.VoltarAoInicio, req.HorarioPlanejadoDeSaida);
        bd.Rotas.Add(rota);
        await bd.SaveChangesAsync(ct);
        return await DetalharAsync(rota, ct);
    }

    public async Task<IReadOnlyList<ResumoDaRotaDto>> ListarAsync(DateOnly? de, DateOnly? ate, CancellationToken ct)
    {
        var inicio = de ?? HorarioDeBrasilia.Hoje(relogio).AddDays(-30);
        var fim = ate ?? HorarioDeBrasilia.Hoje(relogio).AddDays(30);
        var rotas = await bd.Rotas.AsNoTracking()
            .Where(r => (r.DonoId == usuario.Id || r.EntregadorId == usuario.Id) && r.Data >= inicio && r.Data <= fim)
            .OrderByDescending(r => r.Data).ThenByDescending(r => r.CriadoEm)
            .Take(200)
            .ToListAsync(ct);
        return await ResumirAsync(rotas, ct);
    }

    public async Task<Resultado<DetalhesDaRotaDto>> ObterAsync(Guid id, CancellationToken ct)
    {
        var rota = await BuscarComPermissaoAsync(id, ct);
        return rota is null ? RotaNaoEncontrada : await DetalharAsync(rota, ct);
    }

    public async Task<Resultado<DetalhesDaRotaDto>> AlterarAsync(Guid id, ConfiguracaoDaRotaRequisicao req, CancellationToken ct)
    {
        var rota = await BuscarComPermissaoAsync(id, ct);
        if (rota is null) return RotaNaoEncontrada;
        rota.AlterarConfiguracao(string.IsNullOrWhiteSpace(req.Nome) ? rota.Nome : req.Nome, req.Veiculo,
            req.Saida.ParaPonto(), req.Saida.Descricao, req.Chegada?.ParaPonto(), req.Chegada?.Descricao,
            req.VoltarAoInicio, req.HorarioPlanejadoDeSaida);
        await bd.SaveChangesAsync(ct);
        return await DetalharAsync(rota, ct);
    }

    public async Task<Resultado> ExcluirAsync(Guid id, CancellationToken ct)
    {
        var rota = await BuscarComPermissaoAsync(id, ct);
        if (rota is null) return RotaNaoEncontrada;
        if (rota.DonoId != usuario.Id && !usuario.GestorDaOrganizacao)
            return Erro.Proibido("rota.nao_eh_dono", "Apenas quem criou a rota pode excluí-la.");
        await paradas.DesvincularDaRotaAsync(rota.Id, ct);
        bd.Rotas.Remove(rota);
        await bd.SaveChangesAsync(ct);
        return Resultado.Ok();
    }

    public async Task<Resultado<OtimizacaoResposta>> OtimizarAsync(Guid id, PosicaoRequisicao posicao, CancellationToken ct)
    {
        var rota = await BuscarComPermissaoAsync(id, ct);
        if (rota is null) return RotaNaoEncontrada;

        var uso = await identidade.ConsumirUsoAsync(usuario.Id, RecursoMedido.Otimizacao, ct);
        if (uso.Falha) return uso.Erro!;

        var plano = await planejador.OtimizarAsync(rota, posicao.ParaPonto(), ct);
        await bd.SaveChangesAsync(ct);
        await AvisarAtualizacaoAsync(rota, "otimizada", ct);
        return new OtimizacaoResposta(await DetalharAsync(rota, ct), plano.Planejadas, plano.SemLocalizacao, plano.ForaDaJanela);
    }

    /// <summary>Reotimiza a partir da posição atual (desvio, trânsito, parada nova no meio do caminho).</summary>
    public async Task<Resultado<OtimizacaoResposta>> RecalcularAsync(Guid id, PosicaoRequisicao posicao, CancellationToken ct)
    {
        var rota = await BuscarComPermissaoAsync(id, ct);
        if (rota is null) return RotaNaoEncontrada;

        var limites = (await identidade.ObterDireitosAsync(rota.EntregadorId, ct)).Limites;
        if (limites.MaximoRecalculosPorRota is { } maximo && rota.Recalculos >= maximo)
            return Erro.LimiteDoPlano("plano.limite_recalculos",
                $"O plano gratuito permite {maximo} recálculo(s) por rota. Assine o Pro para recálculos ilimitados.");

        var plano = await planejador.OtimizarAsync(rota, posicao.ParaPonto(), ct);
        rota.RegistrarRecalculo();
        await bd.SaveChangesAsync(ct);
        await AvisarAtualizacaoAsync(rota, "recalculada", ct);
        return new OtimizacaoResposta(await DetalharAsync(rota, ct), plano.Planejadas, plano.SemLocalizacao, plano.ForaDaJanela);
    }

    /// <summary>Ordem definida à mão pelo entregador (arrastar e soltar).</summary>
    public async Task<Resultado<DetalhesDaRotaDto>> ReordenarAsync(Guid id, ReordenarRequisicao req, CancellationToken ct)
    {
        var rota = await BuscarComPermissaoAsync(id, ct);
        if (rota is null) return RotaNaoEncontrada;

        var porId = (await paradas.ListarParaPlanejamentoAsync(rota.Id, ct)).ToDictionary(p => p.Id);
        var ordenadas = req.ParadaIds.Where(porId.ContainsKey).Select(i => porId[i])
            .Where(p => p.Status == StatusDaParada.Pendente).ToList();
        ordenadas.AddRange(porId.Values.Where(p => p.Status == StatusDaParada.Pendente && !req.ParadaIds.Contains(p.Id)));

        var atual = new PosicaoRequisicao(req.Latitude, req.Longitude).ParaPonto();
        await planejador.AplicarOrdemAsync(rota, ordenadas, atual ?? rota.Saida,
            planejador.HorarioDeSaida(rota, atual is not null), "manual", ct);
        await bd.SaveChangesAsync(ct);
        await AvisarAtualizacaoAsync(rota, "reordenada", ct);
        return await DetalharAsync(rota, ct);
    }

    public async Task<Resultado<DetalhesDaRotaDto>> IniciarAsync(Guid id, CancellationToken ct)
    {
        var rota = await BuscarComPermissaoAsync(id, ct);
        if (rota is null) return RotaNaoEncontrada;
        rota.Iniciar(relogio.GetUtcNow());
        await bd.SaveChangesAsync(ct);
        return await DetalharAsync(rota, ct);
    }

    public async Task<Resultado<DetalhesDaRotaDto>> ConcluirAsync(Guid id, CancellationToken ct)
    {
        var rota = await BuscarComPermissaoAsync(id, ct);
        if (rota is null) return RotaNaoEncontrada;
        rota.Concluir(relogio.GetUtcNow());
        await bd.SaveChangesAsync(ct);
        return await DetalharAsync(rota, ct);
    }

    /// <summary>Previsão de chegada atualizada pela posição atual: 1 consulta ao motor + trechos já planejados.</summary>
    public async Task<Resultado<PrevisaoDeChegadaResposta>> PreverChegadasAsync(Guid id, PontoGeo atual, CancellationToken ct)
    {
        var rota = await BuscarComPermissaoAsync(id, ct);
        if (rota is null) return RotaNaoEncontrada;

        var porId = (await paradas.ListarParaPlanejamentoAsync(rota.Id, ct)).ToDictionary(p => p.Id);
        var pendentes = rota.Sequencia
            .Where(i => porId.TryGetValue(i.ParadaId, out var p) && p.Status == StatusDaParada.Pendente && p.Local is not null)
            .OrderBy(i => i.Ordem).ToList();
        var agora = relogio.GetUtcNow();
        if (pendentes.Count == 0) return new PrevisaoDeChegadaResposta(agora, null, []);

        var primeiroTrecho = await motor.ObterTrajetoAsync([atual, porId[pendentes[0].ParadaId].Local!.Value],
            rota.Veiculo, comPassos: false, ct);
        var horario = agora.AddSeconds(primeiroTrecho.DuracaoSegundos);
        var previsoes = new List<PrevisaoDaParadaDto>();
        for (var i = 0; i < pendentes.Count; i++)
        {
            if (i > 0) horario = horario.AddSeconds(pendentes[i].DuracaoDoTrechoSegundos);
            if (porId[pendentes[i].ParadaId].NovaTentativaApos is { } naoAntesDe && horario < naoAntesDe) horario = naoAntesDe;
            previsoes.Add(new PrevisaoDaParadaDto(pendentes[i].ParadaId, pendentes[i].Ordem, horario));
            horario = horario.AddSeconds(pendentes[i].AtendimentoSegundos);
        }
        return new PrevisaoDeChegadaResposta(agora, horario, previsoes);
    }

    /// <summary>Navegação curva a curva dentro do app até a próxima parada (Pro).</summary>
    public async Task<Resultado<NavegacaoResposta>> NavegarAsync(Guid id, PontoGeo atual, Guid? paradaId, CancellationToken ct)
    {
        var limites = (await identidade.ObterDireitosAsync(usuario.Id, ct)).Limites;
        if (!limites.NavegacaoNoApp)
            return Erro.LimiteDoPlano("plano.navegacao_no_app",
                "A navegação dentro do app está disponível no plano Pro. No plano gratuito use Waze ou Google Maps.");

        var rota = await BuscarComPermissaoAsync(id, ct);
        if (rota is null) return RotaNaoEncontrada;

        var paradasDaRota = await paradas.ListarDaRotaAsync(rota.Id, ct);
        var destino = paradaId is { } pid
            ? paradasDaRota.FirstOrDefault(p => p.Id == pid)
            : rota.Sequencia.OrderBy(i => i.Ordem)
                .Select(i => paradasDaRota.FirstOrDefault(p => p.Id == i.ParadaId))
                .FirstOrDefault(p => p is { Status: StatusDaParada.Pendente, Local: not null });
        if (destino?.Local is not { } localDoDestino)
            return Erro.NaoEncontrado("rota.sem_proxima_parada", "Nenhuma parada pendente com localização.");

        var trajeto = await motor.ObterTrajetoAsync([atual, localDoDestino], rota.Veiculo, comPassos: true, ct);
        var trecho = trajeto.Trechos.FirstOrDefault();
        var passos = trecho?.Passos.Select(p => new PassoDeNavegacaoDto(p.Instrucao, p.Manobra, p.Modificador, p.NomeDaVia,
            p.DistanciaMetros, p.DuracaoSegundos, p.Local.Latitude, p.Local.Longitude, p.Saida)).ToList() ?? [];
        return new NavegacaoResposta(destino.Id, destino.Endereco.Descricao, trajeto.DistanciaMetros, trajeto.DuracaoSegundos,
            trecho?.Geometria ?? trajeto.Geometria, trajeto.Motor, passos);
    }

    /// <summary>Ordem para carregar o veículo: a última entrega vai no fundo.</summary>
    public async Task<Resultado<IReadOnlyList<ItemDeCarregamentoDto>>> OrdemDeCarregamentoAsync(Guid id, CancellationToken ct)
    {
        var rota = await BuscarComPermissaoAsync(id, ct);
        if (rota is null) return RotaNaoEncontrada;
        var porId = (await paradas.ListarDaRotaAsync(rota.Id, ct)).ToDictionary(p => p.Id);
        IReadOnlyList<ItemDeCarregamentoDto> itens = rota.Sequencia.OrderByDescending(i => i.Ordem)
            .Where(i => porId.TryGetValue(i.ParadaId, out var p) && p.Status == StatusDaParada.Pendente)
            .Select((i, posicao) =>
            {
                var p = porId[i.ParadaId];
                return new ItemDeCarregamentoDto(posicao + 1, i.Ordem, p.Id, p.Endereco.Descricao, p.NomeDoDestinatario,
                    p.CodigosDePacote, p.QuantidadeDePacotes);
            })
            .ToList();
        return Resultado<IReadOnlyList<ItemDeCarregamentoDto>>.Ok(itens);
    }

    internal async Task<Rota?> BuscarComPermissaoAsync(Guid id, CancellationToken ct)
    {
        var rota = await bd.Rotas.FirstOrDefaultAsync(r => r.Id == id, ct);
        return rota is not null && rota.ParaAcesso().PodeSerAcessadaPor(usuario.Id, usuario.OrganizacaoId, usuario.GestorDaOrganizacao)
            ? rota
            : null;
    }

    internal async Task<IReadOnlyList<ResumoDaRotaDto>> ResumirAsync(IReadOnlyList<Rota> rotas, CancellationToken ct)
    {
        var contagens = await paradas.ContarPorRotasAsync(rotas.Select(r => r.Id).ToList(), ct);
        return rotas.Select(r => Resumo(r, contagens.GetValueOrDefault(r.Id))).ToList();
    }

    internal static ResumoDaRotaDto Resumo(Rota r, ContagemDaRota? c) => new(
        r.Id, r.Nome, r.Data, r.Status, r.Veiculo, r.EntregadorId, r.DistanciaTotalMetros, r.DuracaoTotalSegundos,
        c?.Total ?? 0, c?.Entregues ?? 0, c?.Pendentes ?? 0, r.OtimizadaEm);

    /// <summary>
    /// Monta a tela da rota: primeiro as já finalizadas (entregues/devolvidas), depois as pendentes na ordem
    /// otimizada e, por último, as incluídas depois da última otimização.
    /// </summary>
    internal async Task<DetalhesDaRotaDto> DetalharAsync(Rota rota, CancellationToken ct)
    {
        var paradasDaRota = await paradas.ListarDaRotaAsync(rota.Id, ct);
        var sequencia = rota.Sequencia.ToDictionary(i => i.ParadaId);

        var finalizadas = paradasDaRota.Where(p => p.Status != StatusDaParada.Pendente)
            .OrderBy(p => p.EntregueEm ?? p.CriadoEm)
            .Select(p => Item(p, null));
        var planejadas = paradasDaRota.Where(p => p.Status == StatusDaParada.Pendente && sequencia.ContainsKey(p.Id))
            .OrderBy(p => sequencia[p.Id].Ordem)
            .Select(p => Item(p, sequencia[p.Id]))
            .ToList();
        var naoPlanejadas = paradasDaRota.Where(p => p.Status == StatusDaParada.Pendente && !sequencia.ContainsKey(p.Id))
            .Select(p => Item(p, null))
            .ToList();

        var contagem = new ContagemDaRota(paradasDaRota.Count, paradasDaRota.Count(p => p.Status == StatusDaParada.Entregue),
            paradasDaRota.Count(p => p.Status == StatusDaParada.Pendente), paradasDaRota.Count(p => p.Tentativas > 0));
        var limites = (await identidade.ObterDireitosAsync(rota.EntregadorId, ct)).Limites;

        return new DetalhesDaRotaDto(
            Resumo(rota, contagem),
            new PontoDto(rota.LatitudeDeSaida, rota.LongitudeDeSaida, rota.DescricaoDaSaida),
            rota.LatitudeDeChegada is { } lat && rota.LongitudeDeChegada is { } lng
                ? new PontoDto(lat, lng, rota.DescricaoDaChegada)
                : null,
            rota.VoltarAoInicio,
            rota.HorarioPlanejadoDeSaida,
            rota.SaidaConsiderada,
            rota.Geometria,
            PrecisaOtimizar: naoPlanejadas.Any(i => i.Parada.Local is not null),
            rota.Recalculos,
            limites.MaximoRecalculosPorRota is { } maximo ? Math.Max(0, maximo - rota.Recalculos) : null,
            rota.AlgoritmoUsado,
            rota.MotorUsado,
            planejadas.FirstOrDefault()?.Parada.Id,
            [.. finalizadas, .. planejadas, .. naoPlanejadas]);

        static ParadaNaRotaDto Item(ParadaDto p, ItemDaSequencia? i) =>
            new(i?.Ordem, i?.PrevisaoDeChegada, i?.DistanciaDoTrechoMetros, i?.DuracaoDoTrechoSegundos,
                i?.AtrasadaParaJanela ?? false, p);
    }

    private Task AvisarAtualizacaoAsync(Rota rota, string motivo, CancellationToken ct) =>
        barramento.PublicarAsync(new RotaAtualizada(rota.Id, rota.EntregadorId, rota.OrganizacaoId, motivo), ct);
}
