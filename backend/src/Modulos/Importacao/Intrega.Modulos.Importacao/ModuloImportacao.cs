using Intrega.Infraestrutura.Modulos;
using Intrega.Infraestrutura.Persistencia;
using Intrega.Modulos.Importacao.Aplicacao;
using Intrega.Modulos.Importacao.Endpoints;
using Intrega.Modulos.Importacao.Infraestrutura;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Intrega.Modulos.Importacao;

/// <summary>Importação de planilhas de endereços (.xlsx/.csv) com reconhecimento automático de colunas.</summary>
public sealed class ModuloImportacao : IModulo
{
    public string Nome => "Importação";

    public void RegistrarServicos(IServiceCollection servicos, IConfiguration configuracao)
    {
        servicos.AdicionarContextoDoModulo<ContextoImportacao>(configuracao, ContextoImportacao.NomeDoSchema);
        servicos.AddSingleton<FilaDeImportacao>();
        servicos.AddHostedService<ProcessadorDeImportacao>();
        servicos.AddScoped<ServicoDeImportacao>();
        servicos.AdicionarManipuladoresDeEventos(typeof(ModuloImportacao).Assembly);
    }

    public void MapearEndpoints(IEndpointRouteBuilder app) => EndpointsImportacao.Mapear(app);

    public Task InicializarAsync(IServiceProvider servicos, CancellationToken ct) =>
        servicos.MigrarSeHabilitadoAsync<ContextoImportacao>(ct);
}
