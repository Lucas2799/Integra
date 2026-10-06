using Intrega.Modulos.Identidade.Contratos;
using Intrega.Nucleo.Dominio;

namespace Intrega.Modulos.Identidade.Dominio;

internal sealed class Usuario : Entidade
{
    private Usuario() { }

    public string Nome { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string HashDaSenha { get; private set; } = default!;
    public string? Telefone { get; private set; }

    /// <summary>Plano individual (B2C). Membros de organização herdam o plano Frota da organização.</summary>
    public Plano Plano { get; private set; }
    public DateTimeOffset? PlanoExpiraEm { get; private set; }
    public bool EmPeriodoDeTeste { get; private set; }

    public Guid? OrganizacaoId { get; private set; }
    public string? PapelNaOrganizacao { get; private set; }

    /// <summary>Novo usuário ganha alguns dias de Pro grátis para conhecer o produto.</summary>
    public static Usuario Cadastrar(string nome, string email, int diasDeTeste) => new()
    {
        Nome = nome.Trim(),
        Email = NormalizarEmail(email),
        Plano = diasDeTeste > 0 ? Plano.Pro : Plano.Gratuito,
        EmPeriodoDeTeste = diasDeTeste > 0,
        PlanoExpiraEm = diasDeTeste > 0 ? DateTimeOffset.UtcNow.AddDays(diasDeTeste) : null
    };

    public static string NormalizarEmail(string email) => email.Trim().ToLowerInvariant();

    public void DefinirHashDaSenha(string hash)
    {
        HashDaSenha = hash;
        MarcarAlteracao();
    }

    public void AtualizarPerfil(string nome, string? telefone)
    {
        Nome = nome.Trim();
        Telefone = string.IsNullOrWhiteSpace(telefone) ? null : telefone.Trim();
        MarcarAlteracao();
    }

    public void AtivarPlano(Plano plano, DateTimeOffset expiraEm)
    {
        Plano = plano;
        PlanoExpiraEm = expiraEm;
        EmPeriodoDeTeste = false;
        MarcarAlteracao();
    }

    /// <summary>Plano vencido volta a ser Gratuito automaticamente.</summary>
    public Plano PlanoEfetivo(DateTimeOffset agora) =>
        Plano != Plano.Gratuito && (PlanoExpiraEm is null || PlanoExpiraEm > agora) ? Plano : Plano.Gratuito;

    public void EntrarNaOrganizacao(Guid organizacaoId, string papel)
    {
        OrganizacaoId = organizacaoId;
        PapelNaOrganizacao = papel;
        MarcarAlteracao();
    }

    public void SairDaOrganizacao()
    {
        OrganizacaoId = null;
        PapelNaOrganizacao = null;
        MarcarAlteracao();
    }
}
