using Intrega.Modulos.Identidade.Contratos;

namespace Intrega.Modulos.Identidade.Dominio;

/// <summary>Compra validada na loja (auditoria e bloqueio de reuso do mesmo comprovante).</summary>
internal sealed class Compra
{
    private Compra() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid UsuarioId { get; private set; }
    public string Plataforma { get; private set; } = default!;
    public string ProdutoId { get; private set; } = default!;
    public string HashDoComprovante { get; private set; } = default!;
    public Plano Plano { get; private set; }
    public DateTimeOffset ExpiraEm { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; } = DateTimeOffset.UtcNow;

    public static Compra Registrar(Guid usuarioId, string plataforma, string produtoId, string hashDoComprovante,
        Plano plano, DateTimeOffset expiraEm) => new()
    {
        UsuarioId = usuarioId,
        Plataforma = plataforma,
        ProdutoId = produtoId,
        HashDoComprovante = hashDoComprovante,
        Plano = plano,
        ExpiraEm = expiraEm
    };
}
