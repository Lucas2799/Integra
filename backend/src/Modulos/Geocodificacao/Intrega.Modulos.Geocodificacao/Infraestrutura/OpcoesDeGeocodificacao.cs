namespace Intrega.Modulos.Geocodificacao.Infraestrutura;

/// <summary>Seção "Geocodificacao" do appsettings.</summary>
public sealed class OpcoesDeGeocodificacao
{
    public const string Secao = "Geocodificacao";

    /// <summary>Exigido pela política de uso do Nominatim/Photon: identifique o app e um contato.</summary>
    public string IdentificacaoDoApp { get; set; } = "Intrega/0.1 (contato@intrega.app)";

    public OpcoesNominatim Nominatim { get; set; } = new();
    public OpcoesDeServico BrasilApi { get; set; } = new() { UrlBase = "https://brasilapi.com.br/" };
    public OpcoesDeServico ViaCep { get; set; } = new() { UrlBase = "https://viacep.com.br/" };
    public OpcoesDeServico Photon { get; set; } = new() { UrlBase = "https://photon.komoot.io/" };

    /// <summary>Validade do cache para resultados automáticos. Correções manuais não expiram.</summary>
    public int DiasDeCache { get; set; } = 180;
}

public class OpcoesDeServico
{
    public string UrlBase { get; set; } = default!;
}

public sealed class OpcoesNominatim : OpcoesDeServico
{
    public OpcoesNominatim() => UrlBase = "https://nominatim.openstreetmap.org/";

    /// <summary>A instância pública aceita no máximo 1 requisição/s. Use 0 com uma instância própria.</summary>
    public int IntervaloMinimoMs { get; set; } = 1100;
}
