namespace Intrega.Nucleo.Dominio;

/// <summary>Base das entidades: Id ordenável por tempo (UUID v7) e datas de criação/alteração.</summary>
public abstract class Entidade
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();
    public DateTimeOffset CriadoEm { get; protected set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset AtualizadoEm { get; protected set; } = DateTimeOffset.UtcNow;

    protected void MarcarAlteracao() => AtualizadoEm = DateTimeOffset.UtcNow;
}
