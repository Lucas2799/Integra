namespace Intrega.Infraestrutura.TempoReal;

/// <summary>
/// Notificações em tempo real para o app. O host implementa com SignalR;
/// os módulos não conhecem a tecnologia de transporte.
/// </summary>
public interface INotificadorTempoReal
{
    Task NotificarUsuarioAsync(Guid usuarioId, string evento, object dados, CancellationToken ct = default);

    /// <summary>Envia para os gestores da organização (painel da frota).</summary>
    Task NotificarGestoresAsync(Guid organizacaoId, string evento, object dados, CancellationToken ct = default);
}

/// <summary>Nomes dos eventos recebidos pelo app via SignalR.</summary>
public static class EventosTempoReal
{
    public const string RotaAtualizada = "rotaAtualizada";
    public const string ProgressoImportacao = "progressoImportacao";
    public const string PosicaoEntregador = "posicaoEntregador";
}
