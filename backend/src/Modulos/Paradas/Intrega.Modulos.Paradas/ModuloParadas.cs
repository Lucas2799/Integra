using Intrega.Infraestrutura.Modulos;
using Intrega.Infraestrutura.Persistencia;
using Intrega.Modulos.Paradas.Aplicacao;
using Intrega.Modulos.Paradas.Contratos;
using Intrega.Modulos.Paradas.Endpoints;
using Intrega.Modulos.Paradas.Infraestrutura;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Intrega.Modulos.Paradas;

/// <summary>Inclusão e ciclo de vida dos destinos: pendente, entregue, nova tentativa, adiada, devolvida.</summary>
public sealed class ModuloParadas : IModulo
{
    public string Nome => "Paradas";

    public void RegistrarServicos(IServiceCollection servicos, IConfiguration configuracao)
    {
        servicos.AdicionarContextoDoModulo<ContextoParadas>(configuracao, ContextoParadas.NomeDoSchema);
        servicos.AddScoped<ApiDoModuloParadas>();
        servicos.AddScoped<IModuloParadas>(sp => sp.GetRequiredService<ApiDoModuloParadas>());
        servicos.AddScoped<ServicoDeParadas>();
        servicos.AdicionarManipuladoresDeEventos(typeof(ModuloParadas).Assembly);
    }

    public void MapearEndpoints(IEndpointRouteBuilder app) => EndpointsParadas.Mapear(app);

    public Task InicializarAsync(IServiceProvider servicos, CancellationToken ct) =>
        servicos.MigrarSeHabilitadoAsync<ContextoParadas>(ct);
}
