using Intrega.Infraestrutura.Modulos;
using Intrega.Infraestrutura.Persistencia;
using Intrega.Modulos.Roteirizacao.Aplicacao;
using Intrega.Modulos.Roteirizacao.Contratos;
using Intrega.Modulos.Roteirizacao.Endpoints;
using Intrega.Modulos.Roteirizacao.Infraestrutura;
using Intrega.Modulos.Roteirizacao.Motor;
using Intrega.Modulos.Roteirizacao.Otimizacao;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Intrega.Modulos.Roteirizacao;

/// <summary>Planejamento da melhor rota, recálculo, previsão de chegada, navegação e despacho da frota.</summary>
public sealed class ModuloRoteirizacao : IModulo
{
    public string Nome => "Roteirização";

    public void RegistrarServicos(IServiceCollection servicos, IConfiguration configuracao)
    {
        servicos.AdicionarContextoDoModulo<ContextoRoteirizacao>(configuracao, ContextoRoteirizacao.NomeDoSchema);
        var secao = configuracao.GetSection(OpcoesDeRoteirizacao.Secao);
        servicos.Configure<OpcoesDeRoteirizacao>(secao);
        var opcoes = secao.Get<OpcoesDeRoteirizacao>() ?? new OpcoesDeRoteirizacao();

        servicos.AddHttpClient<MotorOsrm>(c =>
            {
                c.Timeout = TimeSpan.FromSeconds(opcoes.Osrm.TempoLimiteSegundos * 3);
                c.DefaultRequestHeaders.UserAgent.ParseAdd("Intrega/0.1");
            })
            .AddStandardResilienceHandler(r =>
            {
                r.AttemptTimeout.Timeout = TimeSpan.FromSeconds(opcoes.Osrm.TempoLimiteSegundos);
                r.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(opcoes.Osrm.TempoLimiteSegundos * 2);
                r.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(opcoes.Osrm.TempoLimiteSegundos * 4);
            });
        servicos.AddSingleton<MotorLinhaReta>();
        servicos.AddScoped<IMotorDeRotas, MotorResiliente>();

        servicos.AddSingleton<OtimizadorOrTools>();
        servicos.AddSingleton<OtimizadorHeuristico>();
        servicos.AddSingleton<IOtimizadorDeRotas, OtimizadorComAlternativa>();

        servicos.AddScoped<PlanejadorDeRotas>();
        servicos.AddScoped<ServicoDeRotas>();
        servicos.AddScoped<ServicoDeFrota>();
        servicos.AddScoped<IModuloRoteirizacao, ApiDoModuloRoteirizacao>();
        servicos.AdicionarManipuladoresDeEventos(typeof(ModuloRoteirizacao).Assembly);
    }

    public void MapearEndpoints(IEndpointRouteBuilder app) => EndpointsRoteirizacao.Mapear(app);

    public Task InicializarAsync(IServiceProvider servicos, CancellationToken ct) =>
        servicos.MigrarSeHabilitadoAsync<ContextoRoteirizacao>(ct);
}
