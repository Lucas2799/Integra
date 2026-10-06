namespace Intrega.Modulos.Identidade.Dominio;

/// <summary>Refresh token: permite renovar o token de acesso sem pedir a senha de novo.</summary>
internal sealed class TokenDeAtualizacao
{
    private TokenDeAtualizacao() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid UsuarioId { get; private set; }
    /// <summary>Somente o hash SHA-256 é guardado; o token em si nunca fica no banco.</summary>
    public string HashDoToken { get; private set; } = default!;
    public DateTimeOffset ExpiraEm { get; private set; }
    public DateTimeOffset? RevogadoEm { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; } = DateTimeOffset.UtcNow;

    public bool Ativo(DateTimeOffset agora) => RevogadoEm is null && ExpiraEm > agora;

    public static TokenDeAtualizacao Criar(Guid usuarioId, string hashDoToken, DateTimeOffset expiraEm) =>
        new() { UsuarioId = usuarioId, HashDoToken = hashDoToken, ExpiraEm = expiraEm };

    public void Revogar() => RevogadoEm = DateTimeOffset.UtcNow;
}
