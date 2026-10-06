using System.Text;
using Intrega.Modulos.Importacao.Leitura;
using Intrega.Modulos.Paradas.Contratos;

namespace Intrega.TestesUnitarios.Importacao;

public class LeitorDePlanilhaTestes
{
    private static PlanilhaLida LerCsv(string conteudo, Encoding? codificacao = null)
    {
        using var stream = new MemoryStream((codificacao ?? Encoding.UTF8).GetBytes(conteudo));
        return LeitorDePlanilha.Ler(stream, "entregas.csv");
    }

    [Fact]
    public void Reconhece_colunas_em_portugues_com_ponto_e_virgula()
    {
        var planilha = LerCsv("""
            Destinatário;Telefone;CEP;Rua;Número;Bairro;Cidade;UF;Código Rastreio
            Maria;11999990000;01310-100;Av. Paulista;1578;Bela Vista;São Paulo;SP;BR123
            """);

        Assert.Single(planilha.Linhas);
        var linha = planilha.Linhas[0];
        Assert.Equal("Maria", linha.NomeDoDestinatario);
        Assert.Equal("Av. Paulista", linha.Logradouro);
        Assert.Equal("1578", linha.Numero);
        Assert.Equal("01310-100", linha.Cep);
        Assert.Equal("BR123", linha.CodigoDoPacote);
        Assert.Empty(planilha.Avisos);
    }

    [Fact]
    public void Coluna_endereco_sem_numero_vira_endereco_completo()
    {
        var planilha = LerCsv("nome,endereco,cep\nJoão,\"Rua A, 10 - Centro\",13010000");

        Assert.Equal("Rua A, 10 - Centro", planilha.Linhas[0].EnderecoCompleto);
        Assert.Null(planilha.Linhas[0].Logradouro);
    }

    [Fact]
    public void Le_csv_salvo_em_latin1_pelo_excel()
    {
        var planilha = LerCsv("destinatário;endereço completo\nJosé;Rua Ação, 5", Encoding.Latin1);

        Assert.Equal("José", planilha.Linhas[0].NomeDoDestinatario);
        Assert.Equal("Rua Ação, 5", planilha.Linhas[0].EnderecoCompleto);
    }

    [Fact]
    public void Avisa_quando_nao_ha_coluna_de_endereco()
    {
        var planilha = LerCsv("nome;telefone\nAna;1199");

        Assert.Contains(planilha.Avisos, a => a.Contains("endereço"));
    }

    [Theory]
    [InlineData("1310100", "01310100")]
    [InlineData("01310-100", "01310100")]
    [InlineData("123", null)]
    public void Corrige_cep_sem_zero_a_esquerda(string entrada, string? esperado) =>
        Assert.Equal(esperado, ConversorDeLinhas.NormalizarCep(entrada));

    [Theory]
    [InlineData("08:30", 8, 30)]
    [InlineData("14h", 14, 0)]
    [InlineData("9h15", 9, 15)]
    public void Le_horarios_em_formatos_brasileiros(string texto, int hora, int minuto) =>
        Assert.Equal(new TimeOnly(hora, minuto), ConversorDeLinhas.LerHorario(texto));

    [Fact]
    public void Converte_linha_com_endereco_completo_extraindo_o_cep()
    {
        var parada = ConversorDeLinhas.Converter(new LinhaImportada
        {
            EnderecoCompleto = "Rua X, 10, Centro, Campinas - SP, 13010-000",
            Marketplace = "Mercado Livre"
        });

        Assert.Equal("13010000", parada.Endereco.Cep);
        Assert.Equal("Rua X, 10, Centro, Campinas - SP, 13010-000", parada.Endereco.TextoLivre);
        Assert.Equal(Marketplace.MercadoLivre, parada.Marketplace);
    }
}
