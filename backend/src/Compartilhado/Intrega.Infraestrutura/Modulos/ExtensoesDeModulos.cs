using System.Reflection;
using Intrega.Infraestrutura.Eventos;
using Intrega.Nucleo.Eventos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Intrega.Infraestrutura.Modulos;

public static class ExtensoesDeModulos
{
    public static IServiceCollection AdicionarModulos(
        this IServiceCollection servicos, IConfiguration configuracao, params IModulo[] modulos)
    {
        servicos.AddSingleton<IReadOnlyList<IModulo>>(modulos);
        servicos.AddScoped<IBarramentoDeEventos, BarramentoDeEventosEmProcesso>();
        foreach (var modulo in modulos)
        {
            modulo.RegistrarServicos(servicos, configuracao);
        }
        return servicos;
    }

    public static IEndpointRouteBuilder MapearModulos(this IEndpointRouteBuilder app)
    {
        foreach (var modulo in app.ServiceProvider.GetRequiredService<IReadOnlyList<IModulo>>())
        {
            modulo.MapearEndpoints(app);
        }
        return app;
    }

    public static async Task InicializarModulosAsync(this WebApplication app, CancellationToken ct = default)
    {
        var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Intrega.Modulos");
        await using var escopo = app.Services.CreateAsyncScope();
        foreach (var modulo in app.Services.GetRequiredService<IReadOnlyList<IModulo>>())
        {
            log.LogInformation("Inicializando módulo {Modulo}", modulo.Nome);
            await modulo.InicializarAsync(escopo.ServiceProvider, ct);
        }
    }

    /// <summary>Registra todos os manipuladores de eventos de integração encontrados no assembly do módulo.</summary>
    public static IServiceCollection AdicionarManipuladoresDeEventos(this IServiceCollection servicos, Assembly assembly)
    {
        var interfaceManipulador = typeof(IManipuladorDeEvento<>);
        foreach (var tipo in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
        {
            foreach (var itf in tipo.GetInterfaces()
                         .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == interfaceManipulador))
            {
                servicos.AddScoped(itf, tipo);
            }
        }
        return servicos;
    }
}
