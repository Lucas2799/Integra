using Intrega.Infraestrutura.Armazenamento;
using Intrega.Modulos.Identidade.Contratos;
using Intrega.Modulos.Paradas.Infraestrutura;
using Intrega.Nucleo.Eventos;
using Microsoft.EntityFrameworkCore;

namespace Intrega.Modulos.Paradas.Aplicacao;

/// <summary>LGPD: apaga as paradas (dados de destinatários) e as fotos do usuário excluído.</summary>
internal sealed class AoExcluirUsuario(ContextoParadas bd, IArmazenamentoDeArquivos armazenamento)
    : IManipuladorDeEvento<UsuarioExcluido>
{
    public async Task TratarAsync(UsuarioExcluido evento, CancellationToken ct)
    {
        await bd.Paradas.Where(p => p.ResponsavelId == evento.UsuarioId).ExecuteDeleteAsync(ct);
        await armazenamento.ExcluirAsync($"comprovantes/{evento.UsuarioId}", ct);
    }
}
