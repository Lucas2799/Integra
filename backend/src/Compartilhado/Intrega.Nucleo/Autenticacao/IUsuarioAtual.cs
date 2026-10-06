namespace Intrega.Nucleo.Autenticacao;

/// <summary>Usuário autenticado na requisição atual (lido do token JWT).</summary>
public interface IUsuarioAtual
{
    bool Autenticado { get; }
    Guid Id { get; }
    string? Email { get; }
    Guid? OrganizacaoId { get; }
    bool GestorDaOrganizacao { get; }
}

/// <summary>Nomes das claims próprias do Intrega dentro do token.</summary>
public static class ClaimsIntrega
{
    public const string OrganizacaoId = "organizacao_id";
    public const string PapelNaOrganizacao = "papel_organizacao";
    public const string PapelGestor = "gestor";
    public const string PapelEntregador = "entregador";
}
