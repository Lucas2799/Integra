using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Intrega.Api.TempoReal;
using Intrega.Infraestrutura.Armazenamento;
using Intrega.Infraestrutura.Modulos;
using Intrega.Infraestrutura.TempoReal;
using Intrega.Infraestrutura.Web;
using Intrega.Modulos.Etiquetas;
using Intrega.Modulos.Geocodificacao;
using Intrega.Modulos.Identidade;
using Intrega.Modulos.Importacao;
using Intrega.Modulos.Paradas;
using Intrega.Modulos.Rastreamento;
using Intrega.Modulos.Roteirizacao;
using Intrega.Nucleo.Autenticacao;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Monólito modular: cada módulo registra seus serviços, seu schema no banco e seus endpoints.
// Para tirar um módulo daqui e virar um serviço separado, basta hospedá-lo em outro projeto como este.
builder.Services.AdicionarModulos(builder.Configuration,
    new ModuloIdentidade(),
    new ModuloGeocodificacao(),
    new ModuloParadas(),
    new ModuloRoteirizacao(),
    new ModuloImportacao(),
    new ModuloEtiquetas(),
    new ModuloRastreamento());

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioAtual, UsuarioAtualHttp>();
builder.Services.AddSingleton<IArmazenamentoDeArquivos, ArmazenamentoEmDiscoLocal>();
builder.Services.AddSingleton<INotificadorTempoReal, NotificadorSignalR>();
builder.Services.AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Enums trafegam como texto ("Pendente", "Entregue"...) para o app ficar legível.
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origens").Get<string[]>() ?? [])
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(PoliticasDeLimiteDeRequisicoes.Autenticacao, contexto => RateLimitPartition.GetFixedWindowLimiter(
        contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/documentacao", o => o.WithTitle("Intrega API"));
}

app.MapHealthChecks("/saude");
app.MapHub<HubIntrega>("/tempo-real");
app.MapearModulos();

await app.InicializarModulosAsync();
await app.RunAsync();

/// <summary>Exposto para testes de integração (WebApplicationFactory).</summary>
public partial class Program;
