using Intrega.Modulos.Geocodificacao.Aplicacao;
using Intrega.Modulos.Geocodificacao.Contratos;

namespace Intrega.TestesUnitarios.Geocodificacao;

public class NormalizadorDeEnderecoTestes
{
    [Fact]
    public void Mesmo_endereco_escrito_de_jeitos_diferentes_gera_a_mesma_chave()
    {
        var a = new EnderecoInformado("Av. Paulista", "1578", Cidade: "São Paulo", Cep: "01310-100");
        var b = new EnderecoInformado("AVENIDA PAULISTA", "1578", Cidade: "sao paulo", Cep: "01310100");

        Assert.Equal(NormalizadorDeEndereco.MontarChave(a), NormalizadorDeEndereco.MontarChave(b));
    }

    [Theory]
    [InlineData("R. das Flores", "rua das flores")]
    [InlineData("Trav. São José", "travessa sao jose")]
    [InlineData("Pça. da Sé", "praca da se")]
    public void Expande_abreviacoes(string entrada, string esperado) =>
        Assert.Equal(esperado, NormalizadorDeEndereco.Simplificar(NormalizadorDeEndereco.ExpandirAbreviacoes(entrada)));

    [Theory]
    [InlineData("São Paulo", "SP")]
    [InlineData("BR-RJ", "RJ")]
    [InlineData("mg", "MG")]
    public void Converte_estado_para_uf(string entrada, string esperado) =>
        Assert.Equal(esperado, UfsBrasileiras.ParaUf(entrada));

    [Fact]
    public void Monta_descricao_legivel()
    {
        var descricao = NormalizadorDeEndereco.MontarDescricao("Rua A", "10", "Apto 2", "Centro", "Campinas", "SP");

        Assert.Equal("Rua A, 10 - Apto 2 - Centro - Campinas/SP", descricao);
    }
}

public class ValidacaoDeResultadoTestes
{
    private static Intrega.Modulos.Geocodificacao.Infraestrutura.Provedores.CandidatoDeGeocodificacao Candidato(string cidade, double lat, double lng) =>
        new(new Intrega.Nucleo.Geo.PontoGeo(lat, lng),
            new EnderecoNormalizado("Rua Augusta", "900", null, null, cidade, "SP", null, ""),
            Intrega.Modulos.Geocodificacao.Infraestrutura.Provedores.PrecisaoDoResultado.Numero, "teste");

    [Theory]
    [InlineData("Rua Augusta, 900, São Paulo", "São Paulo")]
    [InlineData("Rua Augusta, 900, São Paulo - SP", "São Paulo")]
    [InlineData("Rua Augusta 900", null)]
    public void Identifica_a_cidade_citada_no_texto(string texto, string? cidade) =>
        Assert.Equal(cidade, Intrega.Modulos.Geocodificacao.Aplicacao.ServicoDeGeocodificacao.CidadeCitada(texto));

    [Fact]
    public void Resultado_em_outra_cidade_e_suspeito()
    {
        var informado = new EnderecoInformado(TextoLivre: "Rua Augusta, 900, São Paulo");

        Assert.True(Intrega.Modulos.Geocodificacao.Aplicacao.ServicoDeGeocodificacao.Suspeito(informado, Candidato("Sumaré", -22.82, -47.26), null));
        Assert.False(Intrega.Modulos.Geocodificacao.Aplicacao.ServicoDeGeocodificacao.Suspeito(informado, Candidato("São Paulo", -23.55, -46.65), null));
    }

    [Fact]
    public void Resultado_longe_da_regiao_da_rota_e_suspeito()
    {
        var informado = new EnderecoInformado("Rua Augusta", "900");
        var saidaDaRota = new Intrega.Nucleo.Geo.PontoGeo(-23.55, -46.63);

        Assert.True(Intrega.Modulos.Geocodificacao.Aplicacao.ServicoDeGeocodificacao.Suspeito(informado, Candidato("Sumaré", -22.82, -47.26), saidaDaRota));
    }
}

public class NumeroNoTextoTestes
{
    [Theory]
    [InlineData("Rua Augusta, 900, São Paulo", "900")]
    [InlineData("Av. Brasil nº 15A - Centro", "15A")]
    [InlineData("Rua X, 01310-100 São Paulo", null)]
    [InlineData("Rua 7 de Setembro", null)]
    [InlineData("Rua 7 de Setembro, 120", "120")]
    public void Extrai_o_numero_sem_confundir_com_o_cep(string texto, string? esperado) =>
        Assert.Equal(esperado, Intrega.Modulos.Geocodificacao.Aplicacao.NormalizadorDeEndereco.NumeroNoTexto(texto));
}
