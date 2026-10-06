using Intrega.Modulos.Identidade.Contratos;
using Intrega.Modulos.Paradas.Contratos;
using Intrega.Modulos.Roteirizacao.Contratos;
using Intrega.Modulos.Roteirizacao.Dominio;
using Intrega.Modulos.Roteirizacao.Infraestrutura;
using Intrega.Modulos.Roteirizacao.Motor;
using Intrega.Modulos.Roteirizacao.Otimizacao;
using Intrega.Nucleo.Autenticacao;
using Intrega.Nucleo.Eventos;
using Intrega.Nucleo.Geo;
using Intrega.Nucleo.Resultados;
using Intrega.Nucleo.Tempo;
using Microsoft.EntityFrameworkCore;

namespace Intrega.Modulos.Roteirizacao.Aplicacao;

/// <summary>B2B: o gestor distribui as paradas sem rota entre os entregadores (roteirização de vários veículos).</summary>
internal sealed class ServicoDeFrota(
    ContextoRoteirizacao bd,
    PlanejadorDeRotas planejador,
    ServicoDeRotas rotas,
    IMotorDeRotas motor,
    IOtimizadorDeRotas otimizador,
    IModuloParadas paradas,
    IModuloIdentidade identidade,
    IUsuarioAtual usuario,
    IBarramentoDeEventos barramento,
    TimeProvider relogio)
{
    public async Task<Resultado<DespachoResposta>> DespacharAsync(DespachoRequisicao req, CancellationToken ct)
    {
        var (organizacaoId, erro) = await ExigirGestorAsync(ct);
        if (erro is not null) return erro;

        var membros = await identidade.ListarMembrosAsync(organizacaoId, ct);
        var idsDosMembros = membros.Select(m => m.UsuarioId).ToHashSet();
        var entregadores = req.EntregadorIds.Distinct().ToList();
        if (entregadores.Any(e => !idsDosMembros.Contains(e)))
            return Erro.Validacao("frota.entregador_invalido", "Todos os entregadores precisam ser membros da organização.");

        var candidatas = (await paradas.ListarPorIdsAsync(req.ParadaIds.Distinct().ToList(), ct))
            .Where(p => p.OrganizacaoId == organizacaoId && p.Status is StatusDaParada.Pendente or StatusDaParada.Adiada)
            .ToList();
        var localizadas = candidatas.Where(p => p.Local is not null).ToList();
        if (localizadas.Count == 0)
            return Erro.Validacao("frota.sem_paradas", "Nenhuma parada válida com localização para despachar.");

        var deposito = req.Deposito.ParaPonto();
        var pontos = new List<PontoGeo> { deposito };
        pontos.AddRange(localizadas.Select(p => p.Local!.Value));
        var matriz = await motor.ObterMatrizAsync(pontos, req.Veiculo, ct);

        var problema = new ProblemaDeRoteirizacao(
            matriz.Tempos,
            localizadas.Select((p, i) => new ParadaDoProblema(i + 1, p.TempoDeAtendimentoSegundos, Prioridade: p.Prioridade)).ToList(),
            entregadores.Select(_ => new VeiculoDoProblema(0, req.VoltarAoDeposito ? 0 : null)).ToList(),
            MaximoDeParadasPorVeiculo: req.MaximoDeParadasPorEntregador);
        var solucao = otimizador.Otimizar(problema, ct);

        var data = req.Data ?? HorarioDeBrasilia.Hoje(relogio);
        var prefixo = string.IsNullOrWhiteSpace(req.PrefixoDoNome) ? $"Rota {data:dd/MM}" : req.PrefixoDoNome.Trim();
        var criadas = new List<Rota>();

        for (var v = 0; v < entregadores.Count; v++)
        {
            var atribuidas = solucao.Rotas[v].Select(i => localizadas[i]).ToList();
            if (atribuidas.Count == 0) continue;

            var nomeDoEntregador = membros.First(m => m.UsuarioId == entregadores[v]).Nome;
            var rota = Rota.Criar(usuario.Id, entregadores[v], organizacaoId, $"{prefixo} - {nomeDoEntregador}", data,
                req.Veiculo, deposito, req.Deposito.Descricao, null, null, req.VoltarAoDeposito, null);
            bd.Rotas.Add(rota);
            await bd.SaveChangesAsync(ct);

            await paradas.AtribuirARotaAsync(atribuidas.Select(p => p.Id).ToList(), rota.Id, ct);
            var paraPlanejar = (await paradas.ListarParaPlanejamentoAsync(rota.Id, ct)).ToDictionary(p => p.Id);
            var ordenadas = atribuidas.Select(p => paraPlanejar[p.Id]).ToList();
            await planejador.AplicarOrdemAsync(rota, ordenadas, deposito, planejador.HorarioDeSaida(rota, false),
                solucao.Algoritmo, ct);
            await bd.SaveChangesAsync(ct);
            criadas.Add(rota);

            await barramento.PublicarAsync(new RotaAtualizada(rota.Id, rota.EntregadorId, organizacaoId, "despachada"), ct);
        }

        var idsAtribuidos = solucao.Rotas.SelectMany(r => r).Select(i => localizadas[i].Id).ToHashSet();
        var naoAtribuidas = candidatas.Where(p => !idsAtribuidos.Contains(p.Id)).Select(p => p.Id).ToList();
        return new DespachoResposta(await rotas.ResumirAsync(criadas, ct), naoAtribuidas);
    }

    public async Task<Resultado<IReadOnlyList<RotaDaFrotaDto>>> ListarAsync(DateOnly? data, CancellationToken ct)
    {
        var (organizacaoId, erro) = await ExigirGestorAsync(ct);
        if (erro is not null) return erro;

        var dia = data ?? HorarioDeBrasilia.Hoje(relogio);
        var lista = await bd.Rotas.AsNoTracking()
            .Where(r => r.OrganizacaoId == organizacaoId && r.Data == dia)
            .OrderBy(r => r.Nome)
            .ToListAsync(ct);
        var resumos = await rotas.ResumirAsync(lista, ct);
        var nomes = (await identidade.ListarMembrosAsync(organizacaoId, ct)).ToDictionary(m => m.UsuarioId, m => m.Nome);
        IReadOnlyList<RotaDaFrotaDto> resultado = resumos
            .Select(r => new RotaDaFrotaDto(r, nomes.GetValueOrDefault(r.EntregadorId, "—")))
            .ToList();
        return Resultado<IReadOnlyList<RotaDaFrotaDto>>.Ok(resultado);
    }

    private async Task<(Guid OrganizacaoId, Erro? Erro)> ExigirGestorAsync(CancellationToken ct)
    {
        var direitos = await identidade.ObterDireitosAsync(usuario.Id, ct);
        if (direitos.OrganizacaoId is not { } organizacaoId || !direitos.GestorDaOrganizacao)
            return (Guid.Empty, Erro.Proibido("frota.nao_eh_gestor", "Apenas o gestor da organização pode despachar rotas."));
        if (!direitos.Limites.GestaoDeFrota)
            return (Guid.Empty, Erro.LimiteDoPlano("plano.frota", "A gestão de frota exige o plano Frota ativo."));
        return (organizacaoId, null);
    }
}
