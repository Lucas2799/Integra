using Intrega.Modulos.Etiquetas.Interpretacao;
using Intrega.Modulos.Paradas.Contratos;

namespace Intrega.TestesUnitarios.Etiquetas;

/// <summary>
/// Etiquetas SINTÉTICAS imitando o layout comum (remetente + destinatário).
/// Quando houver etiquetas reais, adicione aqui o texto do OCR delas como novos casos.
/// </summary>
public class InterpretadorDeEtiquetasTestes
{
    private const string EtiquetaShopee = """
        SPX Express
        BR2412345678901
        REMETENTE
        Loja Exemplo LTDA
        Rua do Comércio, 50
        04538-133 São Paulo - SP
        DESTINATÁRIO
        MARIA DA SILVA SANTOS
        Avenida Paulista, 1578 - Apto 12
        Bela Vista
        01310-200 São Paulo - SP
        Tel: (11) 98765-4321
        """;

    [Fact]
    public void Extrai_destinatario_e_ignora_o_remetente()
    {
        var etiqueta = InterpretadorDeEtiquetas.Interpretar(EtiquetaShopee, null);

        Assert.Equal(Marketplace.Shopee, etiqueta.Marketplace);
        Assert.Equal("BR2412345678901", etiqueta.CodigoDeRastreio);
        Assert.Equal("Maria da Silva Santos", etiqueta.NomeDoDestinatario);
        Assert.Equal("Avenida Paulista", etiqueta.Endereco.Logradouro);
        Assert.Equal("1578", etiqueta.Endereco.Numero);
        Assert.Equal("Apto 12", etiqueta.Endereco.Complemento);
        Assert.Equal("Bela Vista", etiqueta.Endereco.Bairro);
        Assert.Equal("01310200", etiqueta.Endereco.Cep);
        Assert.Equal("São Paulo", etiqueta.Endereco.Cidade);
        Assert.Equal("SP", etiqueta.Endereco.Uf);
        Assert.Equal("11987654321", etiqueta.Telefone);
        Assert.False(etiqueta.PrecisaConferir);
    }

    [Fact]
    public void Le_o_codigo_do_qr_em_json_do_mercado_livre()
    {
        var codigos = new List<CodigoLido> { new("qr", """{"id":"43219876543","sender_id":123456,"hash_code":"abc"}""") };
        var texto = """
            Mercado Envios
            Destinatário: JOÃO PEREIRA
            R. das Flores, 45
            Centro
            CEP 13010-000 Campinas/SP
            """;

        var etiqueta = InterpretadorDeEtiquetas.Interpretar(texto, codigos);

        Assert.Equal(Marketplace.MercadoLivre, etiqueta.Marketplace);
        Assert.Equal("43219876543", etiqueta.CodigoDeRastreio);
        Assert.Equal("João Pereira", etiqueta.NomeDoDestinatario);
        Assert.Equal("R. das Flores", etiqueta.Endereco.Logradouro);
        Assert.Equal("45", etiqueta.Endereco.Numero);
        Assert.Equal("13010000", etiqueta.Endereco.Cep);
        Assert.Equal("Campinas", etiqueta.Endereco.Cidade);
    }

    [Fact]
    public void Reconhece_rastreio_dos_correios_pelo_codigo_de_barras()
    {
        var codigos = new List<CodigoLido> { new("code128", "QB123456789BR") };

        var etiqueta = InterpretadorDeEtiquetas.Interpretar("Rua A, 10\n20040-002 Rio de Janeiro - RJ", codigos);

        Assert.Equal("QB123456789BR", etiqueta.CodigoDeRastreio);
        Assert.Equal(Marketplace.Correios, etiqueta.Marketplace);
    }

    [Fact]
    public void Sem_marcador_de_destinatario_pede_conferencia()
    {
        var etiqueta = InterpretadorDeEtiquetas.Interpretar("Rua B, 99\n01001-000 São Paulo - SP", null);

        Assert.True(etiqueta.PrecisaConferir);
        Assert.Contains(etiqueta.Avisos, a => a.Contains("Destinatário"));
    }

    [Fact]
    public void Texto_vazio_nao_quebra()
    {
        var etiqueta = InterpretadorDeEtiquetas.Interpretar("", null);

        Assert.Null(etiqueta.Endereco.Cep);
        Assert.Equal(0, etiqueta.Confianca);
    }
}
