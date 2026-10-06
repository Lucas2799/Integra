using Intrega.Modulos.Identidade.Dominio;
using Intrega.Modulos.Identidade.Infraestrutura;
using Intrega.Nucleo.Autenticacao;
using Intrega.Nucleo.Resultados;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Intrega.Modulos.Identidade.Aplicacao;

internal sealed class ServicoDeOrganizacoes(
    ContextoIdentidade bd,
    ApiDoModuloIdentidade api,
    ServicoDeAutenticacao autenticacao,
    IOptions<OpcoesDeAssinatura> assinatura)
{
    private static readonly Erro NaoEhGestor =
        Erro.Proibido("organizacao.nao_eh_gestor", "Apenas o gestor da organização pode fazer isso.");

    private static readonly Erro JaEhMembro =
        Erro.Conflito("organizacao.ja_eh_membro", "Você já faz parte de uma organização.");

    /// <summary>Cria a organização e torna o criador gestor. Devolve nova sessão porque as claims mudaram.</summary>
    public async Task<Resultado<SessaoResposta>> CriarAsync(Guid usuarioId, CriarOrganizacaoRequisicao req, CancellationToken ct)
    {
        var usuario = await bd.Usuarios.FirstAsync(u => u.Id == usuarioId, ct);
        if (usuario.OrganizacaoId is not null) return JaEhMembro;

        var organizacao = Organizacao.Criar(req.Nome, usuarioId, assinatura.Value.DiasDeTesteFrota);
        bd.Organizacoes.Add(organizacao);
        usuario.EntrarNaOrganizacao(organizacao.Id, ClaimsIntrega.PapelGestor);
        api.Invalidar(usuarioId);
        return await autenticacao.EmitirSessaoAsync(usuario, ct);
    }

    public async Task<Resultado<SessaoResposta>> EntrarAsync(Guid usuarioId, EntrarNaOrganizacaoRequisicao req, CancellationToken ct)
    {
        var codigo = req.CodigoDeConvite.Trim().ToUpperInvariant();
        var organizacao = await bd.Organizacoes.FirstOrDefaultAsync(o => o.CodigoDeConvite == codigo, ct);
        if (organizacao is null) return Erro.NaoEncontrado("organizacao.convite_invalido", "Código de convite inválido.");

        var usuario = await bd.Usuarios.FirstAsync(u => u.Id == usuarioId, ct);
        if (usuario.OrganizacaoId is not null) return JaEhMembro;

        usuario.EntrarNaOrganizacao(organizacao.Id, ClaimsIntrega.PapelEntregador);
        api.Invalidar(usuarioId);
        return await autenticacao.EmitirSessaoAsync(usuario, ct);
    }

    public async Task<Resultado<OrganizacaoDto>> ObterMinhaAsync(Guid usuarioId, CancellationToken ct)
    {
        var usuario = await bd.Usuarios.AsNoTracking().FirstAsync(u => u.Id == usuarioId, ct);
        if (usuario.OrganizacaoId is not { } organizacaoId)
            return Erro.NaoEncontrado("organizacao.nenhuma", "Você não faz parte de uma organização.");

        var organizacao = await bd.Organizacoes.AsNoTracking().FirstAsync(o => o.Id == organizacaoId, ct);
        var gestor = usuario.PapelNaOrganizacao == ClaimsIntrega.PapelGestor;
        var membros = await api.ListarMembrosAsync(organizacaoId, ct);
        // Só o gestor vê o código de convite.
        return new OrganizacaoDto(organizacao.Id, organizacao.Nome, gestor ? organizacao.CodigoDeConvite : null,
            organizacao.PlanoExpiraEm, membros);
    }

    public async Task<Resultado<string>> GerarNovoConviteAsync(Guid usuarioId, CancellationToken ct)
    {
        var (organizacao, erro) = await ObterOrganizacaoGerenciadaAsync(usuarioId, ct);
        if (erro is not null) return erro;
        organizacao!.GerarNovoCodigoDeConvite();
        await bd.SaveChangesAsync(ct);
        return organizacao.CodigoDeConvite;
    }

    public async Task<Resultado> RemoverMembroAsync(Guid usuarioId, Guid membroId, CancellationToken ct)
    {
        var (organizacao, erro) = await ObterOrganizacaoGerenciadaAsync(usuarioId, ct);
        if (erro is not null) return erro;
        if (membroId == organizacao!.DonoId)
            return Erro.Validacao("organizacao.dono_nao_pode_sair", "O dono da organização não pode ser removido.");

        var membro = await bd.Usuarios.FirstOrDefaultAsync(u => u.Id == membroId && u.OrganizacaoId == organizacao.Id, ct);
        if (membro is null) return Erro.NaoEncontrado("organizacao.membro_nao_encontrado", "Membro não encontrado.");

        membro.SairDaOrganizacao();
        await bd.SaveChangesAsync(ct);
        api.Invalidar(membroId);
        return Resultado.Ok();
    }

    private async Task<(Organizacao?, Erro?)> ObterOrganizacaoGerenciadaAsync(Guid usuarioId, CancellationToken ct)
    {
        var usuario = await bd.Usuarios.AsNoTracking().FirstAsync(u => u.Id == usuarioId, ct);
        if (usuario.OrganizacaoId is null || usuario.PapelNaOrganizacao != ClaimsIntrega.PapelGestor) return (null, NaoEhGestor);
        var organizacao = await bd.Organizacoes.FirstAsync(o => o.Id == usuario.OrganizacaoId, ct);
        return (organizacao, null);
    }
}
