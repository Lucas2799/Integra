using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Intrega.Infraestrutura.Modulos;

/// <summary>
/// Ponto de entrada de cada módulo do monólito. O host (Intrega.Api) só conhece esta interface.
/// Para extrair um módulo como microsserviço no futuro, basta hospedá-lo em outro host com o mesmo IModulo.
/// </summary>
public interface IModulo
{
    string Nome { get; }

    void RegistrarServicos(IServiceCollection servicos, IConfiguration configuracao);

    void MapearEndpoints(IEndpointRouteBuilder app);

    /// <summary>Executado na inicialização (ex.: aplicar as migrations do schema do módulo).</summary>
    Task InicializarAsync(IServiceProvider servicos, CancellationToken ct) => Task.CompletedTask;
}
