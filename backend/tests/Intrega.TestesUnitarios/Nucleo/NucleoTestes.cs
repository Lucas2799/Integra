using Intrega.Modulos.Identidade.Contratos;
using Intrega.Modulos.Identidade.Dominio;
using Intrega.Nucleo.Geo;

namespace Intrega.TestesUnitarios.Nucleo;

public class NucleoTestes
{
    [Fact]
    public void Polilinha_codifica_e_decodifica_sem_perder_precisao()
    {
        var pontos = new List<PontoGeo> { new(-23.55052, -46.63331), new(-23.56141, -46.65588), new(-22.90685, -43.17290) };

        var decodificados = Polilinha.Decodificar(Polilinha.Codificar(pontos));

        Assert.Equal(pontos.Count, decodificados.Count);
        for (var i = 0; i < pontos.Count; i++)
        {
            Assert.Equal(pontos[i].Latitude, decodificados[i].Latitude, 5);
            Assert.Equal(pontos[i].Longitude, decodificados[i].Longitude, 5);
        }
    }

    [Fact]
    public void Polilinha_bate_com_o_exemplo_oficial_do_google() =>
        Assert.Equal("_p~iF~ps|U_ulLnnqC_mqNvxq`@",
            Polilinha.Codificar([new PontoGeo(38.5, -120.2), new PontoGeo(40.7, -120.95), new PontoGeo(43.252, -126.453)]));

    [Fact]
    public void Distancia_entre_sao_paulo_e_rio_e_aproximadamente_360_km()
    {
        var km = new PontoGeo(-23.5505, -46.6333).DistanciaAte(new PontoGeo(-22.9068, -43.1729)) / 1000;

        Assert.InRange(km, 355, 365);
    }

    [Fact]
    public void Plano_pro_vencido_volta_a_ser_gratuito()
    {
        var usuario = Usuario.Cadastrar("Ana", "ana@exemplo.com", diasDeTeste: 7);

        Assert.Equal(Plano.Pro, usuario.PlanoEfetivo(DateTimeOffset.UtcNow));
        Assert.Equal(Plano.Gratuito, usuario.PlanoEfetivo(DateTimeOffset.UtcNow.AddDays(8)));
    }

    [Fact]
    public void Plano_gratuito_limita_e_pro_libera()
    {
        Assert.Equal(25, CatalogoDePlanos.Gratuito.MaximoParadasPorRota);
        Assert.Null(CatalogoDePlanos.Pro.MaximoParadasPorRota);
        Assert.True(CatalogoDePlanos.Frota.GestaoDeFrota);
        Assert.False(CatalogoDePlanos.Pro.GestaoDeFrota);
    }
}
