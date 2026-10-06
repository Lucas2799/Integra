using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Intrega.Modulos.Identidade.Dominio;
using Intrega.Nucleo.Autenticacao;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Intrega.Modulos.Identidade.Infraestrutura;

internal sealed record TokensEmitidos(
    string TokenDeAcesso, DateTimeOffset AcessoExpiraEm, string TokenDeAtualizacao, DateTimeOffset AtualizacaoExpiraEm);

internal sealed class ServicoDeTokens(IOptions<OpcoesDeAutenticacao> opcoes, TimeProvider relogio)
{
    private readonly OpcoesDeAutenticacao _opcoes = opcoes.Value;

    public TokensEmitidos Emitir(Usuario usuario, out TokenDeAtualizacao tokenParaSalvar)
    {
        var agora = relogio.GetUtcNow();
        var acessoExpiraEm = agora.AddMinutes(_opcoes.MinutosDoTokenDeAcesso);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Email),
            new(JwtRegisteredClaimNames.Name, usuario.Nome)
        };
        if (usuario.OrganizacaoId is { } organizacaoId)
        {
            claims.Add(new Claim(ClaimsIntrega.OrganizacaoId, organizacaoId.ToString()));
            claims.Add(new Claim(ClaimsIntrega.PapelNaOrganizacao, usuario.PapelNaOrganizacao ?? ClaimsIntrega.PapelEntregador));
        }

        var tokenDeAcesso = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _opcoes.Emissor,
            Audience = _opcoes.Publico,
            Subject = new ClaimsIdentity(claims),
            NotBefore = agora.UtcDateTime,
            Expires = acessoExpiraEm.UtcDateTime,
            SigningCredentials = new SigningCredentials(ChaveDeAssinatura(_opcoes), SecurityAlgorithms.HmacSha256)
        });

        var tokenDeAtualizacao = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(48));
        var atualizacaoExpiraEm = agora.AddDays(_opcoes.DiasDoTokenDeAtualizacao);
        tokenParaSalvar = TokenDeAtualizacao.Criar(usuario.Id, Hash(tokenDeAtualizacao), atualizacaoExpiraEm);

        return new TokensEmitidos(tokenDeAcesso, acessoExpiraEm, tokenDeAtualizacao, atualizacaoExpiraEm);
    }

    public static SymmetricSecurityKey ChaveDeAssinatura(OpcoesDeAutenticacao opcoes) =>
        new(Encoding.UTF8.GetBytes(opcoes.ChaveDeAssinatura));

    public static string Hash(string valor) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(valor)));
}
