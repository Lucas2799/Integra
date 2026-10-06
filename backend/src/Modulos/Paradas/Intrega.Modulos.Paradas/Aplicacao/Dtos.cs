using System.ComponentModel.DataAnnotations;
using Intrega.Modulos.Geocodificacao.Contratos;
using Intrega.Modulos.Paradas.Contratos;
using Intrega.Nucleo.Geo;

namespace Intrega.Modulos.Paradas.Aplicacao;

public sealed record DadosDaParadaRequisicao(
    [property: Required] EnderecoInformado Endereco,
    double? Latitude = null,
    double? Longitude = null,
    [property: StringLength(120)] string? NomeDoDestinatario = null,
    [property: StringLength(30)] string? TelefoneDoDestinatario = null,
    [property: StringLength(500)] string? Observacoes = null,
    List<string>? CodigosDePacote = null,
    [property: Range(1, 200)] int QuantidadeDePacotes = 1,
    Marketplace Marketplace = Marketplace.Desconhecido,
    TimeOnly? JanelaInicio = null,
    TimeOnly? JanelaFim = null,
    [property: Range(0, 10)] int Prioridade = 0,
    [property: Range(15, 3600)] int? TempoDeAtendimentoSegundos = null)
{
    public NovaParada ParaNovaParada() => new(Endereco, NomeDoDestinatario, TelefoneDoDestinatario, Observacoes,
        CodigosDePacote, QuantidadeDePacotes, Marketplace, JanelaInicio, JanelaFim, Prioridade, TempoDeAtendimentoSegundos,
        Latitude is { } lat && Longitude is { } lng ? new PontoGeo(lat, lng) : null);
}

public sealed record CriarParadaRequisicao(Guid? RotaId, [property: Required] DadosDaParadaRequisicao Parada);

public sealed record ConfirmarLocalRequisicao(
    [property: Range(-90, 90)] double Latitude,
    [property: Range(-180, 180)] double Longitude);

public sealed record EntregarRequisicao(
    [property: StringLength(120)] string? RecebidoPor = null,
    [property: StringLength(500)] string? Observacoes = null,
    double? Latitude = null,
    double? Longitude = null,
    /// <summary>Quando a entrega foi feita offline, o app envia o horário real.</summary>
    DateTimeOffset? EntregueEm = null);

public sealed record RegistrarFalhaRequisicao(
    MotivoDaFalha Motivo,
    EstrategiaDeNovaTentativa Estrategia,
    [property: Range(5, 600)] int? MinutosParaNovaTentativa = null,
    [property: StringLength(500)] string? Observacoes = null,
    double? Latitude = null,
    double? Longitude = null);

public sealed record MoverParadaRequisicao(Guid? RotaId);
