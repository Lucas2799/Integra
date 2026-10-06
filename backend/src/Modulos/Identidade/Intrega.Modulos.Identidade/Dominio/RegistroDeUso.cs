using Intrega.Modulos.Identidade.Contratos;

namespace Intrega.Modulos.Identidade.Dominio;

/// <summary>Contador diário de uso por recurso: base dos limites do plano gratuito.</summary>
internal sealed class RegistroDeUso
{
    private RegistroDeUso() { }

    public Guid UsuarioId { get; private set; }
    public RecursoMedido Recurso { get; private set; }
    public DateOnly Dia { get; private set; }
    public int Quantidade { get; private set; }

    public static RegistroDeUso Iniciar(Guid usuarioId, RecursoMedido recurso, DateOnly dia) =>
        new() { UsuarioId = usuarioId, Recurso = recurso, Dia = dia };

    public void Incrementar() => Quantidade++;
}
