using Intrega.Modulos.Geocodificacao.Contratos;
using Intrega.Nucleo.Geo;

namespace Intrega.Modulos.Geocodificacao.Infraestrutura.Provedores;

// Interfaces dos provedores externos. Trocar Nominatim por Google/HERE/Mapbox
// é implementar uma destas interfaces e mudar o registro no ModuloGeocodificacao.

internal enum PrecisaoDoResultado
{
    Cep,
    Bairro,
    Rua,
    Numero
}

internal sealed record CandidatoDeGeocodificacao(PontoGeo Local, EnderecoNormalizado Endereco, PrecisaoDoResultado Precisao, string Provedor);

internal interface IProvedorDeCep
{
    string Nome { get; }
    Task<InformacoesDoCep?> ConsultarAsync(string cep, CancellationToken ct);
}

internal interface IGeocodificador
{
    Task<CandidatoDeGeocodificacao?> GeocodificarPorCamposAsync(EnderecoInformado endereco, PontoGeo? proximoDe, CancellationToken ct);
    Task<CandidatoDeGeocodificacao?> GeocodificarTextoAsync(string texto, PontoGeo? proximoDe, CancellationToken ct);
}

internal interface IGeocodificadorReverso
{
    Task<EnderecoNormalizado?> ReversoAsync(PontoGeo ponto, CancellationToken ct);
}

internal interface IProvedorDeBusca
{
    Task<IReadOnlyList<SugestaoDeEndereco>> BuscarAsync(string texto, PontoGeo? proximoDe, CancellationToken ct);
}
