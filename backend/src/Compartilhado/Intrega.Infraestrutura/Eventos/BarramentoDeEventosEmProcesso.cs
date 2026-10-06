using Intrega.Nucleo.Eventos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Intrega.Infraestrutura.Eventos;

/// <summary>
/// Barramento em processo: cada manipulador roda no próprio escopo de DI (DbContext isolado),
/// imitando um broker de mensagens. A falha de um manipulador não derruba os demais.
/// Para garantia de entrega (at-least-once) troque por Outbox + RabbitMQ quando houver necessidade.
/// </summary>
public sealed class BarramentoDeEventosEmProcesso(
    IServiceScopeFactory fabricaDeEscopos,
    ILogger<BarramentoDeEventosEmProcesso> log) : IBarramentoDeEventos
{
    public async Task PublicarAsync<TEvento>(TEvento evento, CancellationToken ct = default)
        where TEvento : IEventoDeIntegracao
    {
        int quantidade;
        await using (var sonda = fabricaDeEscopos.CreateAsyncScope())
        {
            quantidade = sonda.ServiceProvider.GetServices<IManipuladorDeEvento<TEvento>>().Count();
        }

        for (var i = 0; i < quantidade; i++)
        {
            await using var escopo = fabricaDeEscopos.CreateAsyncScope();
            var manipulador = escopo.ServiceProvider.GetServices<IManipuladorDeEvento<TEvento>>().ElementAt(i);
            try
            {
                await manipulador.TratarAsync(evento, ct);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Falha no manipulador {Manipulador} do evento {Evento}",
                    manipulador.GetType().Name, typeof(TEvento).Name);
            }
        }
    }
}
