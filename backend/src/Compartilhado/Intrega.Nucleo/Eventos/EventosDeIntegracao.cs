namespace Intrega.Nucleo.Eventos;

/// <summary>Evento trocado entre módulos. Fica no projeto .Contratos do módulo que publica.</summary>
public interface IEventoDeIntegracao
{
    Guid EventoId { get; }
    DateTimeOffset OcorridoEm { get; }
}

public abstract record EventoDeIntegracao : IEventoDeIntegracao
{
    public Guid EventoId { get; init; } = Guid.CreateVersion7();
    public DateTimeOffset OcorridoEm { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Reage a um evento publicado por outro módulo.</summary>
public interface IManipuladorDeEvento<in TEvento> where TEvento : IEventoDeIntegracao
{
    Task TratarAsync(TEvento evento, CancellationToken ct);
}

/// <summary>
/// Barramento de eventos. A implementação atual roda em processo (custo zero);
/// pode ser trocada por RabbitMQ/Azure Service Bus sem alterar os módulos.
/// </summary>
public interface IBarramentoDeEventos
{
    Task PublicarAsync<TEvento>(TEvento evento, CancellationToken ct = default) where TEvento : IEventoDeIntegracao;
}
