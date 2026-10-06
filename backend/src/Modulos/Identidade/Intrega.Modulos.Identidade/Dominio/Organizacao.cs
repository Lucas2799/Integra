using System.Security.Cryptography;
using Intrega.Nucleo.Dominio;

namespace Intrega.Modulos.Identidade.Dominio;

/// <summary>Empresa/transportadora (B2B). O gestor convida entregadores pelo código de convite.</summary>
internal sealed class Organizacao : Entidade
{
    // Sem caracteres ambíguos (0/O, 1/I) para facilitar digitar o código.
    private const string AlfabetoDoConvite = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private Organizacao() { }

    public string Nome { get; private set; } = default!;
    public Guid DonoId { get; private set; }
    public string CodigoDeConvite { get; private set; } = default!;
    public DateTimeOffset? PlanoExpiraEm { get; private set; }

    public static Organizacao Criar(string nome, Guid donoId, int diasDeTeste) => new()
    {
        Nome = nome.Trim(),
        DonoId = donoId,
        CodigoDeConvite = NovoCodigoDeConvite(),
        PlanoExpiraEm = DateTimeOffset.UtcNow.AddDays(diasDeTeste)
    };

    public bool PlanoAtivo(DateTimeOffset agora) => PlanoExpiraEm is null || PlanoExpiraEm > agora;

    public void GerarNovoCodigoDeConvite()
    {
        CodigoDeConvite = NovoCodigoDeConvite();
        MarcarAlteracao();
    }

    public void EstenderPlano(DateTimeOffset expiraEm)
    {
        PlanoExpiraEm = expiraEm;
        MarcarAlteracao();
    }

    private static string NovoCodigoDeConvite() =>
        string.Create(8, 0, (span, _) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = AlfabetoDoConvite[RandomNumberGenerator.GetInt32(AlfabetoDoConvite.Length)];
        });
}
