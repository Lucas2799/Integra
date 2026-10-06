using Intrega.Modulos.Identidade.Dominio;
using Intrega.Modulos.Identidade.Infraestrutura;
using Intrega.Nucleo.Resultados;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Intrega.Modulos.Identidade.Aplicacao;

internal sealed class ServicoDeAutenticacao(
    ContextoIdentidade bd,
    ServicoDeTokens tokens,
    IPasswordHasher<Usuario> hasher,
    IOptions<OpcoesDeAssinatura> assinatura,
    TimeProvider relogio)
{
    private static readonly Erro CredenciaisInvalidas =
        Erro.Validacao("autenticacao.credenciais_invalidas", "E-mail ou senha inválidos.");

    public async Task<Resultado<SessaoResposta>> CadastrarAsync(CadastroRequisicao req, CancellationToken ct)
    {
        var email = Usuario.NormalizarEmail(req.Email);
        if (await bd.Usuarios.AnyAsync(u => u.Email == email, ct))
            return Erro.Conflito("autenticacao.email_em_uso", "Já existe uma conta com este e-mail.");

        var usuario = Usuario.Cadastrar(req.Nome, email, assinatura.Value.DiasDeTeste);
        usuario.DefinirHashDaSenha(hasher.HashPassword(usuario, req.Senha));
        bd.Usuarios.Add(usuario);
        return await EmitirSessaoAsync(usuario, ct);
    }

    public async Task<Resultado<SessaoResposta>> EntrarAsync(EntrarRequisicao req, CancellationToken ct)
    {
        var email = Usuario.NormalizarEmail(req.Email);
        var usuario = await bd.Usuarios.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (usuario is null) return CredenciaisInvalidas;

        var verificacao = hasher.VerifyHashedPassword(usuario, usuario.HashDaSenha, req.Senha);
        if (verificacao == PasswordVerificationResult.Failed) return CredenciaisInvalidas;
        if (verificacao == PasswordVerificationResult.SuccessRehashNeeded)
            usuario.DefinirHashDaSenha(hasher.HashPassword(usuario, req.Senha));

        return await EmitirSessaoAsync(usuario, ct);
    }

    /// <summary>Renova a sessão. O token de atualização é trocado a cada uso (o antigo é revogado).</summary>
    public async Task<Resultado<SessaoResposta>> RenovarAsync(TokenDeAtualizacaoRequisicao req, CancellationToken ct)
    {
        var hash = ServicoDeTokens.Hash(req.TokenDeAtualizacao);
        var salvo = await bd.TokensDeAtualizacao.FirstOrDefaultAsync(t => t.HashDoToken == hash, ct);
        if (salvo is null || !salvo.Ativo(relogio.GetUtcNow()))
            return Erro.Validacao("autenticacao.sessao_expirada", "Sessão expirada. Entre novamente.");

        var usuario = await bd.Usuarios.FirstOrDefaultAsync(u => u.Id == salvo.UsuarioId, ct);
        if (usuario is null) return Erro.NaoEncontrado("autenticacao.usuario_nao_encontrado", "Usuário não encontrado.");

        salvo.Revogar();
        return await EmitirSessaoAsync(usuario, ct);
    }

    public async Task SairAsync(TokenDeAtualizacaoRequisicao req, CancellationToken ct)
    {
        var hash = ServicoDeTokens.Hash(req.TokenDeAtualizacao);
        var salvo = await bd.TokensDeAtualizacao.FirstOrDefaultAsync(t => t.HashDoToken == hash, ct);
        if (salvo is null) return;
        salvo.Revogar();
        await bd.SaveChangesAsync(ct);
    }

    /// <summary>Emite tokens novos. Usado também quando as claims mudam (ex.: entrar numa organização).</summary>
    public async Task<SessaoResposta> EmitirSessaoAsync(Usuario usuario, CancellationToken ct)
    {
        var emitidos = tokens.Emitir(usuario, out var tokenParaSalvar);
        bd.TokensDeAtualizacao.Add(tokenParaSalvar);
        await bd.SaveChangesAsync(ct);
        return new SessaoResposta(emitidos.TokenDeAcesso, emitidos.AcessoExpiraEm,
            emitidos.TokenDeAtualizacao, emitidos.AtualizacaoExpiraEm);
    }
}
