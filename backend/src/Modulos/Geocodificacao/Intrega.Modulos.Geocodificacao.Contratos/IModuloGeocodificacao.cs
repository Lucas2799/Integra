using Intrega.Nucleo.Geo;

namespace Intrega.Modulos.Geocodificacao.Contratos;

/// <summary>Endereço como veio do usuário, da planilha ou da etiqueta. Todos os campos são opcionais.</summary>
public sealed record EnderecoInformado(
    string? Logradouro = null,
    string? Numero = null,
    string? Complemento = null,
    string? Bairro = null,
    string? Cidade = null,
    string? Uf = null,
    string? Cep = null,
    /// <summary>Endereço em texto corrido, usado quando os campos não vêm separados.</summary>
    string? TextoLivre = null);

public sealed record EnderecoNormalizado(
    string? Logradouro,
    string? Numero,
    string? Complemento,
    string? Bairro,
    string? Cidade,
    string? Uf,
    string? Cep,
    /// <summary>Texto pronto para exibir: "Rua X, 10 - Bairro - Cidade/UF".</summary>
    string Descricao);

/// <summary>
/// Confiança de 0 a 1. Acima de 0,85 o número da casa foi encontrado; entre 0,6 e 0,85, só a rua.
/// Abaixo de 0,6 (só CEP/bairro) o app pede para o entregador confirmar o pino no mapa.
/// </summary>
public sealed record ResultadoDeGeocodificacao(PontoGeo Local, double Confianca, string Provedor, EnderecoNormalizado Endereco)
{
    public const double LimiteParaConfirmacao = 0.6;
    public bool PrecisaConfirmacao => Confianca < LimiteParaConfirmacao;
}

public sealed record InformacoesDoCep(
    string Cep,
    string? Logradouro,
    string? Bairro,
    string Cidade,
    string Uf,
    PontoGeo? Local);

public sealed record SugestaoDeEndereco(string Descricao, EnderecoNormalizado Endereco, PontoGeo Local);

public interface IModuloGeocodificacao
{
    /// <param name="proximoDe">Região esperada (ex.: saída da rota). Resultados muito longe dela perdem confiança.</param>
    Task<ResultadoDeGeocodificacao?> GeocodificarAsync(EnderecoInformado endereco, PontoGeo? proximoDe = null, CancellationToken ct = default);

    Task<InformacoesDoCep?> ConsultarCepAsync(string cep, CancellationToken ct = default);

    Task<IReadOnlyList<SugestaoDeEndereco>> BuscarAsync(string texto, PontoGeo? proximoDe, CancellationToken ct = default);

    Task<EnderecoNormalizado?> GeocodificacaoReversaAsync(PontoGeo ponto, CancellationToken ct = default);

    /// <summary>Correção manual do pino pelo entregador. Passa a valer em todas as próximas entregas no endereço.</summary>
    Task SalvarCorrecaoAsync(EnderecoInformado endereco, PontoGeo local, CancellationToken ct = default);
}
