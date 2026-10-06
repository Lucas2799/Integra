using Intrega.Infraestrutura.Modulos;
using Intrega.Infraestrutura.Persistencia;
using Intrega.Modulos.Rastreamento.Aplicacao;
using Intrega.Modulos.Rastreamento.Endpoints;
using Intrega.Modulos.Rastreamento.Infraestrutura;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Intrega.Modulos.Rastreamento;

/// <summary>Posições GPS, contabilização do tempo entre entregas, produtividade e painel financeiro.</summary>
public sealed class ModuloRastreamento : IModulo
{
    public string Nome => "Rastreamento";

    public void RegistrarServicos(IServiceCollection servicos, IConfiguration configuracao)
    {
        servicos.AdicionarContextoDoModulo<ContextoRastreamento>(configuracao, ContextoRastreamento.NomeDoSchema);
        servicos.Configure<OpcoesDeRastreamento>(configuracao.GetSection(OpcoesDeRastreamento.Secao));
        servicos.AddScoped<ServicoDeRastreamento>();
        servicos.AddHostedService<LimpezaDePosicoesAntigas>();
        servicos.AdicionarManipuladoresDeEventos(typeof(ModuloRastreamento).Assembly);
    }

    public void MapearEndpoints(IEndpointRouteBuilder app) => EndpointsRastreamento.Mapear(app);

    public Task InicializarAsync(IServiceProvider servicos, CancellationToken ct) =>
        servicos.MigrarSeHabilitadoAsync<ContextoRastreamento>(ct);
}
