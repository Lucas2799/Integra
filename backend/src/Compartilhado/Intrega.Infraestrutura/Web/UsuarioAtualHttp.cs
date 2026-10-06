using System.Security.Claims;
using Intrega.Nucleo.Autenticacao;
using Microsoft.AspNetCore.Http;

namespace Intrega.Infraestrutura.Web;

/// <summary>Lê o usuário autenticado a partir das claims do token JWT da requisição.</summary>
public sealed class UsuarioAtualHttp(IHttpContextAccessor acessor) : IUsuarioAtual
{
    private ClaimsPrincipal? Principal => acessor.HttpContext?.User;

    public bool Autenticado => Principal?.Identity?.IsAuthenticated == true;

    public Guid Id =>
        Guid.TryParse(Principal?.FindFirstValue("sub") ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new UnauthorizedAccessException("Usuário não autenticado.");

    public string? Email => Principal?.FindFirstValue("email") ?? Principal?.FindFirstValue(ClaimTypes.Email);

    public Guid? OrganizacaoId =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimsIntrega.OrganizacaoId), out var id) ? id : null;

    public bool GestorDaOrganizacao =>
        Principal?.FindFirstValue(ClaimsIntrega.PapelNaOrganizacao) == ClaimsIntrega.PapelGestor;
}
