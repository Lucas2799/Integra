using Intrega.Modulos.Geocodificacao.Contratos;
using Intrega.Nucleo.Eventos;
using Intrega.Nucleo.Geo;
using Intrega.Nucleo.Resultados;

namespace Intrega.Modulos.Paradas.Contratos;

public enum StatusDaParada
{
    /// <summary>A visitar (inclui nova tentativa depois de ausência).</summary>
    Pendente,
    Entregue,
    /// <summary>Adiada para outro dia: sai da rota e volta para a lista de paradas sem rota.</summary>
    Adiada,
    /// <summary>Devolvida ao remetente: encerrada sem entrega.</summary>
    Devolvida
}

public enum MotivoDaFalha
{
    DestinatarioAusente,
    Recusada,
    EnderecoNaoEncontrado,
    AcessoNegado,
    Avariada,
    Outro
}

/// <summary>O que fazer quando a entrega não acontece.</summary>
public enum EstrategiaDeNovaTentativa
{
    /// <summary>Tenta de novo depois de todas as outras paradas.</summary>
    FimDaRota,
    /// <summary>Tenta de novo depois de X minutos, encaixada na rota.</summary>
    AposMinutos,
    ProximoDia,
    DevolverAoRemetente
}

public enum OrigemDaParada
{
    Manual,
    Planilha,
    Etiqueta
}

public enum Marketplace
{
    Desconhecido,
    Shopee,
    MercadoLivre,
    Amazon,
    Magalu,
    Shein,
    AliExpress,
    Correios,
    Outro
}

public sealed record ResponsavelPelaParada(Guid UsuarioId, Guid? OrganizacaoId);

public sealed record NovaParada(
    EnderecoInformado Endereco,
    string? NomeDoDestinatario = null,
    string? TelefoneDoDestinatario = null,
    string? Observacoes = null,
    IReadOnlyList<string>? CodigosDePacote = null,
    int QuantidadeDePacotes = 1,
    Marketplace Marketplace = Marketplace.Desconhecido,
    TimeOnly? JanelaInicio = null,
    TimeOnly? JanelaFim = null,
    int Prioridade = 0,
    int? TempoDeAtendimentoSegundos = null,
    /// <summary>Coordenada já conhecida (ex.: escolhida no autocompletar); dispensa a geocodificação.</summary>
    PontoGeo? Local = null);

/// <summary>Visão mínima de uma parada usada pelo planejador de rotas.</summary>
public sealed record ParadaParaPlanejamento(
    Guid Id,
    PontoGeo? Local,
    int TempoDeAtendimentoSegundos,
    TimeOnly? JanelaInicio,
    TimeOnly? JanelaFim,
    int Prioridade,
    StatusDaParada Status,
    int Tentativas,
    EstrategiaDeNovaTentativa? EstrategiaDeNovaTentativa,
    DateTimeOffset? NovaTentativaApos);

public sealed record ParadaDto(
    Guid Id,
    Guid ResponsavelId,
    Guid? OrganizacaoId,
    Guid? RotaId,
    StatusDaParada Status,
    EnderecoNormalizado Endereco,
    PontoGeo? Local,
    double? ConfiancaDaLocalizacao,
    bool LocalConfirmado,
    bool PrecisaConfirmarLocal,
    string? NomeDoDestinatario,
    string? TelefoneDoDestinatario,
    string? Observacoes,
    IReadOnlyList<string> CodigosDePacote,
    int QuantidadeDePacotes,
    Marketplace Marketplace,
    OrigemDaParada Origem,
    TimeOnly? JanelaInicio,
    TimeOnly? JanelaFim,
    int Prioridade,
    int TempoDeAtendimentoSegundos,
    int Tentativas,
    MotivoDaFalha? UltimoMotivoDeFalha,
    EstrategiaDeNovaTentativa? EstrategiaDeNovaTentativa,
    DateTimeOffset? NovaTentativaApos,
    DateTimeOffset? EntregueEm,
    string? RecebidoPor,
    bool TemComprovante,
    DateTimeOffset CriadoEm);

public sealed record ErroNaInclusao(int Indice, string Mensagem);

public sealed record ResultadoDaInclusao(
    int Criadas,
    int PrecisamConfirmacao,
    int SemLocalizacao,
    IReadOnlyList<Guid> IdsCriados,
    IReadOnlyList<ErroNaInclusao> Erros);

public sealed record ContagemDaRota(int Total, int Entregues, int Pendentes, int ComFalha);

public interface IModuloParadas
{
    Task<IReadOnlyList<ParadaParaPlanejamento>> ListarParaPlanejamentoAsync(Guid rotaId, CancellationToken ct = default);

    Task<IReadOnlyList<ParadaDto>> ListarDaRotaAsync(Guid rotaId, CancellationToken ct = default);

    Task<IReadOnlyList<ParadaDto>> ListarPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>Cria paradas geocodificando cada endereço. Respeita o limite de paradas por rota do plano.</summary>
    Task<Resultado<ResultadoDaInclusao>> IncluirAsync(ResponsavelPelaParada responsavel, Guid? rotaId,
        IReadOnlyList<NovaParada> paradas, OrigemDaParada origem, Action<int>? aoProgredir = null, CancellationToken ct = default);

    Task<ParadaDto?> BuscarPorCodigoDePacoteAsync(Guid responsavelId, string codigo, CancellationToken ct = default);

    /// <summary>Usado no despacho da frota: move paradas sem rota para a rota de cada entregador.</summary>
    Task<int> AtribuirARotaAsync(IReadOnlyCollection<Guid> paradaIds, Guid rotaId, CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, ContagemDaRota>> ContarPorRotasAsync(IReadOnlyCollection<Guid> rotaIds, CancellationToken ct = default);

    /// <summary>Rota excluída: as paradas pendentes voltam para a lista de paradas sem rota.</summary>
    Task DesvincularDaRotaAsync(Guid rotaId, CancellationToken ct = default);
}

public sealed record ParadaEntregue(Guid ParadaId, Guid? RotaId, Guid UsuarioId, DateTimeOffset EntregueEm,
    PontoGeo? Local, Marketplace Marketplace, int QuantidadeDePacotes) : EventoDeIntegracao;

public sealed record EntregaNaoRealizada(Guid ParadaId, Guid? RotaId, Guid UsuarioId, MotivoDaFalha Motivo,
    EstrategiaDeNovaTentativa Estrategia, DateTimeOffset? NovaTentativaApos, PontoGeo? LocalAtual) : EventoDeIntegracao;
