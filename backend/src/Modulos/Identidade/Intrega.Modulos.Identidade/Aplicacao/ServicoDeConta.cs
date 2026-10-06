using Intrega.Modulos.Identidade.Contratos;
using Intrega.Modulos.Identidade.Infraestrutura;
using Intrega.Nucleo.Eventos;
using Intrega.Nucleo.Resultados;
using Microsoft.EntityFrameworkCore;

namespace Intrega.Modulos.Identidade.Aplicacao;

internal sealed class ServicoDeConta(ContextoIdentidade bd, ApiDoModuloIdentidade api, IBarramentoDeEventos barramento)
{
    private static readonly Erro ContaNaoEncontrada = Erro.NaoEncontrado("conta.nao_encontrada", "Conta não encontrada.");

    public async Task<Resultado<ContaResposta>> ObterAsync(Guid usuarioId, CancellationToken ct)
    {
        var usuario = await bd.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == usuarioId, ct);
        if (usuario is null) return ContaNaoEncontrada;

        var direitos = await api.ObterDireitosAsync(usuarioId, ct);
        var uso = await api.ObterUsoDeHojeAsync(usuarioId, ct);

        ResumoDaOrganizacaoDto? organizacao = null;
        if (usuario.OrganizacaoId is { } organizacaoId)
        {
            var nome = await bd.Organizacoes.Where(o => o.Id == organizacaoId).Select(o => o.Nome).FirstOrDefaultAsync(ct);
            if (nome is not null)
                organizacao = new ResumoDaOrganizacaoDto(organizacaoId, nome, usuario.PapelNaOrganizacao ?? "entregador");
        }

        return new ContaResposta(usuario.Id, usuario.Nome, usuario.Email, usuario.Telefone, direitos.Plano,
            direitos.PlanoExpiraEm, usuario.EmPeriodoDeTeste && direitos.Plano == Plano.Pro, direitos.Limites, uso, organizacao);
    }

    public async Task<Resultado> AtualizarPerfilAsync(Guid usuarioId, AtualizarPerfilRequisicao req, CancellationToken ct)
    {
        var usuario = await bd.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct);
        if (usuario is null) return ContaNaoEncontrada;
        usuario.AtualizarPerfil(req.Nome, req.Telefone);
        await bd.SaveChangesAsync(ct);
        return Resultado.Ok();
    }

    /// <summary>Exclusão de conta (LGPD art. 18). Os outros módulos apagam seus dados ao receber <see cref="UsuarioExcluido"/>.</summary>
    public async Task<Resultado> ExcluirAsync(Guid usuarioId, CancellationToken ct)
    {
        var usuario = await bd.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct);
        if (usuario is null) return Resultado.Ok();

        var donoDeOrganizacaoComMembros = await bd.Organizacoes
            .Where(o => o.DonoId == usuarioId)
            .AnyAsync(o => bd.Usuarios.Any(u => u.OrganizacaoId == o.Id && u.Id != usuarioId), ct);
        if (donoDeOrganizacaoComMembros)
            return Erro.Conflito("conta.dono_de_organizacao", "Remova os membros da organização antes de excluir a conta.");

        await bd.TokensDeAtualizacao.Where(t => t.UsuarioId == usuarioId).ExecuteDeleteAsync(ct);
        await bd.RegistrosDeUso.Where(r => r.UsuarioId == usuarioId).ExecuteDeleteAsync(ct);
        await bd.Organizacoes.Where(o => o.DonoId == usuarioId).ExecuteDeleteAsync(ct);
        bd.Usuarios.Remove(usuario);
        await bd.SaveChangesAsync(ct);
        api.Invalidar(usuarioId);

        await barramento.PublicarAsync(new UsuarioExcluido(usuarioId), ct);
        return Resultado.Ok();
    }
}
