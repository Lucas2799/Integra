using Intrega.Nucleo.Eventos;
using Intrega.Nucleo.Resultados;

namespace Intrega.Modulos.Identidade.Contratos;

/// <summary>API pública do módulo Identidade, usada pelos outros módulos.</summary>
public interface IModuloIdentidade
{
    Task<DireitosDoUsuario> ObterDireitosAsync(Guid usuarioId, CancellationToken ct = default);

    /// <summary>Consome uma unidade do limite diário. Falha com <see cref="TipoErro.LimiteDoPlano"/> se esgotado.</summary>
    Task<Resultado> ConsumirUsoAsync(Guid usuarioId, RecursoMedido recurso, CancellationToken ct = default);

    Task<IReadOnlyList<MembroDaOrganizacaoDto>> ListarMembrosAsync(Guid organizacaoId, CancellationToken ct = default);
}

public sealed record MembroDaOrganizacaoDto(Guid UsuarioId, string Nome, string Email, string Papel);

/// <summary>Publicado quando o usuário exclui a conta (LGPD). Cada módulo apaga os próprios dados.</summary>
public sealed record UsuarioExcluido(Guid UsuarioId) : EventoDeIntegracao;
