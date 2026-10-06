using Intrega.Infraestrutura.Modulos;
using Intrega.Infraestrutura.Persistencia;
using Intrega.Modulos.Geocodificacao.Aplicacao;
using Intrega.Modulos.Geocodificacao.Contratos;
using Intrega.Modulos.Geocodificacao.Endpoints;
using Intrega.Modulos.Geocodificacao.Infraestrutura;
using Intrega.Modulos.Geocodificacao.Infraestrutura.Provedores;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Intrega.Modulos.Geocodificacao;

/// <summary>Endereço → coordenada, com provedores gratuitos (BrasilAPI, ViaCEP, Nominatim, Photon), todos substituíveis.</summary>
public sealed class ModuloGeocodificacao : IModulo
{
    public string Nome => "Geocodificação";

    public void RegistrarServicos(IServiceCollection servicos, IConfiguration configuracao)
    {
        servicos.AdicionarContextoDoModulo<ContextoGeocodificacao>(configuracao, ContextoGeocodificacao.NomeDoSchema);
        var secao = configuracao.GetSection(OpcoesDeGeocodificacao.Secao);
        servicos.Configure<OpcoesDeGeocodificacao>(secao);
        var opcoes = secao.Get<OpcoesDeGeocodificacao>() ?? new OpcoesDeGeocodificacao();

        void Configurar(HttpClient cliente, string urlBase)
        {
            cliente.BaseAddress = new Uri(urlBase);
            cliente.DefaultRequestHeaders.UserAgent.ParseAdd(opcoes.IdentificacaoDoApp);
        }

        // A ordem de registro define a ordem de tentativa na consulta de CEP.
        servicos.AddHttpClient<ProvedorDeCepBrasilApi>(c => Configurar(c, opcoes.BrasilApi.UrlBase)).AddStandardResilienceHandler();
        servicos.AddHttpClient<ProvedorDeCepViaCep>(c => Configurar(c, opcoes.ViaCep.UrlBase)).AddStandardResilienceHandler();
        servicos.AddScoped<IProvedorDeCep>(sp => sp.GetRequiredService<ProvedorDeCepBrasilApi>());
        servicos.AddScoped<IProvedorDeCep>(sp => sp.GetRequiredService<ProvedorDeCepViaCep>());

        servicos.AddSingleton<LimitadorNominatim>();
        servicos.AddHttpClient<GeocodificadorNominatim>(c => Configurar(c, opcoes.Nominatim.UrlBase)).AddStandardResilienceHandler();
        servicos.AddScoped<IGeocodificador>(sp => sp.GetRequiredService<GeocodificadorNominatim>());
        servicos.AddScoped<IGeocodificadorReverso>(sp => sp.GetRequiredService<GeocodificadorNominatim>());

        servicos.AddHttpClient<BuscaPhoton>(c => Configurar(c, opcoes.Photon.UrlBase)).AddStandardResilienceHandler();
        servicos.AddScoped<IProvedorDeBusca>(sp => sp.GetRequiredService<BuscaPhoton>());

        servicos.AddScoped<IModuloGeocodificacao, ServicoDeGeocodificacao>();
    }

    public void MapearEndpoints(IEndpointRouteBuilder app) => EndpointsGeocodificacao.Mapear(app);

    public Task InicializarAsync(IServiceProvider servicos, CancellationToken ct) =>
        servicos.MigrarSeHabilitadoAsync<ContextoGeocodificacao>(ct);
}
