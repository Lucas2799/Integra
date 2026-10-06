using Intrega.Modulos.Rastreamento.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Intrega.Modulos.Rastreamento.Aplicacao;

/// <summary>LGPD: uma vez por dia apaga as posições GPS mais antigas que o prazo de retenção.</summary>
internal sealed class LimpezaDePosicoesAntigas(
    IServiceScopeFactory fabricaDeEscopos,
    IOptions<OpcoesDeRastreamento> opcoes,
    TimeProvider relogio,
    ILogger<LimpezaDePosicoesAntigas> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var temporizador = new PeriodicTimer(TimeSpan.FromHours(24), relogio);
        do
        {
            try
            {
                await using var escopo = fabricaDeEscopos.CreateAsyncScope();
                var bd = escopo.ServiceProvider.GetRequiredService<ContextoRastreamento>();
                var limite = relogio.GetUtcNow().AddDays(-opcoes.Value.DiasDeRetencaoDasPosicoes);
                var apagadas = await bd.Posicoes.Where(p => p.RegistradaEm < limite).ExecuteDeleteAsync(ct);
                if (apagadas > 0) log.LogInformation("Limpeza LGPD: {Quantidade} posições antigas apagadas", apagadas);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                log.LogWarning(ex, "Falha na limpeza de posições antigas");
            }
        } while (await temporizador.WaitForNextTickAsync(ct));
    }
}
