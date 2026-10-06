using Intrega.Modulos.Roteirizacao.Motor;

namespace Intrega.TestesUnitarios.Roteirizacao;

public class GeradorDeInstrucoesTestes
{
    [Theory]
    [InlineData("turn", "right", "Rua Augusta", null, "Vire à direita na Rua Augusta")]
    [InlineData("turn", "left", "Viaduto do Chá", null, "Vire à esquerda no Viaduto do Chá")]
    [InlineData("depart", null, "Avenida Paulista", null, "Siga pela Avenida Paulista")]
    [InlineData("roundabout", null, "Rua X", 2, "Na rotatória, pegue a 2ª saída para Rua X")]
    [InlineData("arrive", "right", null, null, "Você chegou. O destino está à direita")]
    [InlineData("turn", "uturn", null, null, "Faça o retorno")]
    [InlineData("fork", "slight left", null, null, "Na bifurcação, mantenha-se à esquerda")]
    public void Gera_instrucoes_em_portugues(string manobra, string? modificador, string? via, int? saida, string esperado) =>
        Assert.Equal(esperado, GeradorDeInstrucoes.Gerar(manobra, modificador, via, saida));
}
