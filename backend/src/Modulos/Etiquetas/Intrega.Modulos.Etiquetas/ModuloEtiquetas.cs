using Intrega.Infraestrutura.Modulos;
using Intrega.Modulos.Etiquetas.Aplicacao;
using Intrega.Modulos.Etiquetas.Endpoints;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Intrega.Modulos.Etiquetas;

/// <summary>Leitura da etiqueta do pacote (QR/código de barras + OCR feito no celular). Não tem banco próprio.</summary>
public sealed class ModuloEtiquetas : IModulo
{
    public string Nome => "Etiquetas";

    public void RegistrarServicos(IServiceCollection servicos, IConfiguration configuracao) =>
        servicos.AddScoped<ServicoDeEtiquetas>();

    public void MapearEndpoints(IEndpointRouteBuilder app) => EndpointsEtiquetas.Mapear(app);
}
