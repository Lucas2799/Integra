using Intrega.Modulos.Identidade.Contratos;
using Intrega.Modulos.Importacao.Infraestrutura;
using Intrega.Nucleo.Eventos;
using Microsoft.EntityFrameworkCore;

namespace Intrega.Modulos.Importacao.Aplicacao;

/// <summary>LGPD: apaga o histórico de importações (contém dados de destinatários).</summary>
internal sealed class AoExcluirUsuario(ContextoImportacao bd) : IManipuladorDeEvento<UsuarioExcluido>
{
    public Task TratarAsync(UsuarioExcluido evento, CancellationToken ct) =>
        bd.Trabalhos.Where(t => t.UsuarioId == evento.UsuarioId).ExecuteDeleteAsync(ct);
}
