using System.ComponentModel.DataAnnotations;
using Intrega.Modulos.Paradas.Contratos;
using Intrega.Modulos.Roteirizacao.Contratos;
using Intrega.Nucleo.Geo;

namespace Intrega.Modulos.Roteirizacao.Aplicacao;

public sealed record PontoRequisicao(
    [property: Range(-90, 90)] double Latitude,
    [property: Range(-180, 180)] double Longitude,
    [property: StringLength(300)] string? Descricao = null)
{
    public PontoGeo ParaPonto() => new(Latitude, Longitude);
}

public sealed record ConfiguracaoDaRotaRequisicao(
    [property: Required] PontoRequisicao Saida,
    [property: StringLength(120)] string? Nome = null,
    DateOnly? Data = null,
    PontoRequisicao? Chegada = null,
    bool VoltarAoInicio = false,
    PerfilDeVeiculo Veiculo = PerfilDeVeiculo.Carro,
    DateTimeOffset? HorarioPlanejadoDeSaida = null);

/// <summary>Posição atual do entregador (opcional). Quando enviada, a rota é calculada a partir dela.</summary>
public sealed record PosicaoRequisicao(double? Latitude = null, double? Longitude = null)
{
    public PontoGeo? ParaPonto() =>
        Latitude is { } lat && Longitude is { } lng && new PontoGeo(lat, lng) is { Valido: true } p ? p : null;
}

public sealed record ReordenarRequisicao([property: Required] List<Guid> ParadaIds, double? Latitude = null, double? Longitude = null);

public sealed record PontoDto(double Latitude, double Longitude, string? Descricao);

public sealed record ResumoDaRotaDto(
    Guid Id,
    string Nome,
    DateOnly Data,
    StatusDaRota Status,
    PerfilDeVeiculo Veiculo,
    Guid EntregadorId,
    double DistanciaTotalMetros,
    double DuracaoTotalSegundos,
    int TotalDeParadas,
    int Entregues,
    int Pendentes,
    DateTimeOffset? OtimizadaEm);

public sealed record ParadaNaRotaDto(
    int? Ordem,
    DateTimeOffset? PrevisaoDeChegada,
    double? DistanciaDoTrechoMetros,
    double? DuracaoDoTrechoSegundos,
    bool AtrasadaParaJanela,
    ParadaDto Parada);

public sealed record DetalhesDaRotaDto(
    ResumoDaRotaDto Resumo,
    PontoDto Saida,
    PontoDto? Chegada,
    bool VoltarAoInicio,
    DateTimeOffset? HorarioPlanejadoDeSaida,
    DateTimeOffset? SaidaConsiderada,
    string? Geometria,
    bool PrecisaOtimizar,
    int Recalculos,
    int? RecalculosRestantes,
    string? AlgoritmoUsado,
    string? MotorUsado,
    Guid? ProximaParadaId,
    IReadOnlyList<ParadaNaRotaDto> Paradas);

public sealed record OtimizacaoResposta(DetalhesDaRotaDto Rota, int Planejadas, int SemLocalizacao, int ForaDaJanela);

public sealed record PrevisaoDaParadaDto(Guid ParadaId, int Ordem, DateTimeOffset PrevisaoDeChegada);

public sealed record PrevisaoDeChegadaResposta(DateTimeOffset CalculadaEm, DateTimeOffset? TerminoPrevisto, IReadOnlyList<PrevisaoDaParadaDto> Paradas);

public sealed record PassoDeNavegacaoDto(
    string Instrucao,
    string Manobra,
    string? Modificador,
    string? NomeDaVia,
    double DistanciaMetros,
    double DuracaoSegundos,
    double Latitude,
    double Longitude,
    int? Saida);

public sealed record NavegacaoResposta(
    Guid ParadaId,
    string DescricaoDaParada,
    double DistanciaMetros,
    double DuracaoSegundos,
    string Geometria,
    string Motor,
    IReadOnlyList<PassoDeNavegacaoDto> Passos);

/// <summary>PosicaoNoCarregamento 1 = carregar primeiro (fundo do veículo) = última entrega.</summary>
public sealed record ItemDeCarregamentoDto(
    int PosicaoNoCarregamento,
    int OrdemDeEntrega,
    Guid ParadaId,
    string Descricao,
    string? NomeDoDestinatario,
    IReadOnlyList<string> CodigosDePacote,
    int QuantidadeDePacotes);

public sealed record DespachoRequisicao(
    [property: Required] PontoRequisicao Deposito,
    [property: Required, MinLength(1)] List<Guid> EntregadorIds,
    [property: Required, MinLength(1)] List<Guid> ParadaIds,
    DateOnly? Data = null,
    bool VoltarAoDeposito = false,
    PerfilDeVeiculo Veiculo = PerfilDeVeiculo.Carro,
    [property: Range(1, 500)] int? MaximoDeParadasPorEntregador = null,
    [property: StringLength(80)] string? PrefixoDoNome = null);

public sealed record DespachoResposta(IReadOnlyList<ResumoDaRotaDto> Rotas, IReadOnlyList<Guid> ParadasNaoAtribuidas);

public sealed record RotaDaFrotaDto(ResumoDaRotaDto Rota, string NomeDoEntregador);
