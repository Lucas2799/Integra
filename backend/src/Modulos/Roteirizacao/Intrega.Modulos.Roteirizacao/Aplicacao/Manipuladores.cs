using Intrega.Infraestrutura.TempoReal;
using Intrega.Modulos.Identidade.Contratos;
using Intrega.Modulos.Paradas.Contratos;
using Intrega.Modulos.Roteirizacao.Contratos;
using Intrega.Modulos.Roteirizacao.Infraestrutura;
using Intrega.Nucleo.Eventos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Intrega.Modulos.Roteirizacao.Aplicacao;

/// <summary>
/// Recálculo automático quando a entrega não acontece (destinatário ausente, recusa...).
/// Dentro do limite do plano: reotimiza tudo a partir da posição atual.
/// Acima do limite (gratuito): só move ou retira a parada, sem reotimizar.
/// </summary>
internal sealed class AoNaoRealizarEntrega(
    ContextoRoteirizacao bd,
    PlanejadorDeRotas planejador,
    IModuloParadas paradas,
    IModuloIdentidade identidade,
    IBarramentoDeEventos barramento,
    ILogger<AoNaoRealizarEntrega> log) : IManipuladorDeEvento<EntregaNaoRealizada>
{
    public async Task TratarAsync(EntregaNaoRealizada evento, CancellationToken ct)
    {
        if (evento.RotaId is not { } rotaId) return;
        var rota = await bd.Rotas.FirstOrDefaultAsync(r => r.Id == rotaId, ct);
        if (rota is null || rota.Status == StatusDaRota.Concluida) return;

        rota.Iniciar(evento.OcorridoEm);
        var limites = (await identidade.ObterDireitosAsync(rota.EntregadorId, ct)).Limites;
        var podeReotimizar = limites.MaximoRecalculosPorRota is not { } maximo || rota.Recalculos < maximo;
        string motivo;

        if (podeReotimizar)
        {
            await planejador.OtimizarAsync(rota, evento.LocalAtual, ct);
            rota.RegistrarRecalculo();
            motivo = "entrega_nao_realizada";
        }
        else
        {
            var porId = (await paradas.ListarParaPlanejamentoAsync(rota.Id, ct)).ToDictionary(p => p.Id);
            var ordenadas = rota.Sequencia.OrderBy(i => i.Ordem)
                .Where(i => i.ParadaId != evento.ParadaId && porId.TryGetValue(i.ParadaId, out var p) && p.Status == StatusDaParada.Pendente)
                .Select(i => porId[i.ParadaId])
                .ToList();
            var voltaPraRota = evento.Estrategia is EstrategiaDeNovaTentativa.FimDaRota or EstrategiaDeNovaTentativa.AposMinutos;
            if (voltaPraRota && porId.TryGetValue(evento.ParadaId, out var naoEntregue)) ordenadas.Add(naoEntregue);
            await planejador.AplicarOrdemAsync(rota, ordenadas, evento.LocalAtual ?? rota.Saida,
                planejador.HorarioDeSaida(rota, true), "ajuste_simples", ct);
            motivo = "entrega_nao_realizada_sem_reotimizar";
        }

        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            log.LogWarning(ex, "Rota {RotaId} foi alterada ao mesmo tempo durante o recálculo", rotaId);
            return;
        }
        await barramento.PublicarAsync(new RotaAtualizada(rota.Id, rota.EntregadorId, rota.OrganizacaoId, motivo), ct);
    }
}

/// <summary>Avança o status da rota conforme as entregas acontecem e conclui quando acabar.</summary>
internal sealed class AoEntregarParada(ContextoRoteirizacao bd, IModuloParadas paradas, IBarramentoDeEventos barramento)
    : IManipuladorDeEvento<ParadaEntregue>
{
    public async Task TratarAsync(ParadaEntregue evento, CancellationToken ct)
    {
        if (evento.RotaId is not { } rotaId) return;
        var rota = await bd.Rotas.FirstOrDefaultAsync(r => r.Id == rotaId, ct);
        if (rota is null) return;

        rota.Iniciar(evento.EntregueEm);
        var pendentes = (await paradas.ListarParaPlanejamentoAsync(rotaId, ct)).Count(p => p.Status == StatusDaParada.Pendente);
        if (pendentes == 0) rota.Concluir(evento.EntregueEm);
        await bd.SaveChangesAsync(ct);

        await barramento.PublicarAsync(new RotaAtualizada(rota.Id, rota.EntregadorId, rota.OrganizacaoId,
            pendentes == 0 ? "concluida" : "progresso"), ct);
    }
}

/// <summary>Avisa em tempo real o app do entregador (e o painel do gestor) que a rota mudou.</summary>
internal sealed class AoAtualizarRota(INotificadorTempoReal notificador) : IManipuladorDeEvento<RotaAtualizada>
{
    public async Task TratarAsync(RotaAtualizada evento, CancellationToken ct)
    {
        var dados = new { evento.RotaId, evento.Motivo };
        await notificador.NotificarUsuarioAsync(evento.EntregadorId, EventosTempoReal.RotaAtualizada, dados, ct);
        if (evento.OrganizacaoId is { } organizacaoId)
            await notificador.NotificarGestoresAsync(organizacaoId, EventosTempoReal.RotaAtualizada, dados, ct);
    }
}

/// <summary>LGPD: apaga as rotas do usuário excluído.</summary>
internal sealed class AoExcluirUsuario(ContextoRoteirizacao bd) : IManipuladorDeEvento<UsuarioExcluido>
{
    public async Task TratarAsync(UsuarioExcluido evento, CancellationToken ct)
    {
        await bd.Rotas.Where(r => r.DonoId == evento.UsuarioId).ExecuteDeleteAsync(ct);
        // Rotas que o gestor criou para o entregador excluído voltam para o gestor.
        await bd.Rotas.Where(r => r.EntregadorId == evento.UsuarioId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.EntregadorId, r => r.DonoId), ct);
    }
}
