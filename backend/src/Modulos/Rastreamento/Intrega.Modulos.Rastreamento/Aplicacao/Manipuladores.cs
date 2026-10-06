using Intrega.Modulos.Identidade.Contratos;
using Intrega.Modulos.Paradas.Contratos;
using Intrega.Modulos.Rastreamento.Dominio;
using Intrega.Modulos.Rastreamento.Infraestrutura;
using Intrega.Nucleo.Eventos;
using Microsoft.EntityFrameworkCore;

namespace Intrega.Modulos.Rastreamento.Aplicacao;

internal sealed class AoEntregarParada(ContextoRastreamento bd) : IManipuladorDeEvento<ParadaEntregue>
{
    public async Task TratarAsync(ParadaEntregue evento, CancellationToken ct)
    {
        bd.RegistrosDeEntrega.Add(RegistroDeEntrega.Entregue(evento));
        await bd.SaveChangesAsync(ct);
    }
}

internal sealed class AoNaoRealizarEntrega(ContextoRastreamento bd) : IManipuladorDeEvento<EntregaNaoRealizada>
{
    public async Task TratarAsync(EntregaNaoRealizada evento, CancellationToken ct)
    {
        bd.RegistrosDeEntrega.Add(RegistroDeEntrega.NaoEntregue(evento));
        await bd.SaveChangesAsync(ct);
    }
}

/// <summary>LGPD: apaga posições, histórico e configurações do usuário excluído.</summary>
internal sealed class AoExcluirUsuario(ContextoRastreamento bd) : IManipuladorDeEvento<UsuarioExcluido>
{
    public async Task TratarAsync(UsuarioExcluido evento, CancellationToken ct)
    {
        await bd.Posicoes.Where(p => p.UsuarioId == evento.UsuarioId).ExecuteDeleteAsync(ct);
        await bd.RegistrosDeEntrega.Where(r => r.UsuarioId == evento.UsuarioId).ExecuteDeleteAsync(ct);
        await bd.ConfiguracoesFinanceiras.Where(c => c.UsuarioId == evento.UsuarioId).ExecuteDeleteAsync(ct);
    }
}
