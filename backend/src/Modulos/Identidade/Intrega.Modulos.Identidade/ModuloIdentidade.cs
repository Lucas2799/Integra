using Intrega.Infraestrutura.Modulos;
using Intrega.Infraestrutura.Persistencia;
using Intrega.Modulos.Identidade.Aplicacao;
using Intrega.Modulos.Identidade.Contratos;
using Intrega.Modulos.Identidade.Dominio;
using Intrega.Modulos.Identidade.Endpoints;
using Intrega.Modulos.Identidade.Infraestrutura;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Intrega.Modulos.Identidade;

/// <summary>Contas, autenticação JWT, organizações (B2B), planos e limites do freemium.</summary>
public sealed class ModuloIdentidade : IModulo
{
    public string Nome => "Identidade";

    public void RegistrarServicos(IServiceCollection servicos, IConfiguration configuracao)
    {
        servicos.AdicionarContextoDoModulo<ContextoIdentidade>(configuracao, ContextoIdentidade.NomeDoSchema);

        servicos.AddOptions<OpcoesDeAutenticacao>().Bind(configuracao.GetSection(OpcoesDeAutenticacao.Secao))
            .ValidateDataAnnotations().ValidateOnStart();
        servicos.AddOptions<OpcoesDeAssinatura>().Bind(configuracao.GetSection(OpcoesDeAssinatura.Secao));

        var jwt = configuracao.GetSection(OpcoesDeAutenticacao.Secao).Get<OpcoesDeAutenticacao>() ?? new OpcoesDeAutenticacao();
        servicos.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opcoes =>
            {
                opcoes.MapInboundClaims = false;
                opcoes.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Emissor,
                    ValidAudience = jwt.Publico,
                    IssuerSigningKey = ServicoDeTokens.ChaveDeAssinatura(jwt),
                    NameClaimType = "name",
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
                // O SignalR envia o token pela query string.
                opcoes.Events = new JwtBearerEvents
                {
                    OnMessageReceived = contexto =>
                    {
                        var token = contexto.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(token) && contexto.HttpContext.Request.Path.StartsWithSegments("/tempo-real"))
                            contexto.Token = token;
                        return Task.CompletedTask;
                    }
                };
            });
        servicos.AddAuthorization();

        servicos.AddMemoryCache();
        servicos.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
        servicos.AddSingleton<ServicoDeTokens>();
        servicos.AddScoped<IVerificadorDeCompraNaLoja, VerificadorDeCompraDeTeste>();
        servicos.AddScoped<ApiDoModuloIdentidade>();
        servicos.AddScoped<IModuloIdentidade>(sp => sp.GetRequiredService<ApiDoModuloIdentidade>());
        servicos.AddScoped<ServicoDeAutenticacao>();
        servicos.AddScoped<ServicoDeConta>();
        servicos.AddScoped<ServicoDeOrganizacoes>();
        servicos.AddScoped<ServicoDeAssinatura>();
    }

    public void MapearEndpoints(IEndpointRouteBuilder app) => EndpointsIdentidade.Mapear(app);

    public Task InicializarAsync(IServiceProvider servicos, CancellationToken ct) =>
        servicos.MigrarSeHabilitadoAsync<ContextoIdentidade>(ct);
}
