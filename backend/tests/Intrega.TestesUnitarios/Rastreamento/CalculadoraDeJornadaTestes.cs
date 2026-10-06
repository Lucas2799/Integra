using Intrega.Modulos.Rastreamento.Aplicacao;
using Intrega.Nucleo.Geo;

namespace Intrega.TestesUnitarios.Rastreamento;

public class CalculadoraDeJornadaTestes
{
    private static readonly DateTimeOffset Inicio = new(2026, 10, 6, 9, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void Soma_deslocamentos_reais_e_ignora_tremidas_do_gps()
    {
        var pontos = new List<PontoNoTempo>
        {
            new(new PontoGeo(-23.5500, -46.6300), Inicio),
            new(new PontoGeo(-23.55001, -46.63001), Inicio.AddSeconds(10)), // ~1,5 m: tremida
            new(new PontoGeo(-23.5590, -46.6300), Inicio.AddSeconds(120)), // ~1 km
        };

        var metros = CalculadoraDeJornada.DistanciaPercorridaMetros(pontos);

        Assert.InRange(metros, 950, 1050);
    }

    [Fact]
    public void Ignora_salto_impossivel_de_gps()
    {
        var pontos = new List<PontoNoTempo>
        {
            new(new PontoGeo(-23.55, -46.63), Inicio),
            new(new PontoGeo(-22.90, -43.17), Inicio.AddSeconds(30)), // Rio de Janeiro em 30 s
        };

        Assert.Equal(0, CalculadoraDeJornada.DistanciaPercorridaMetros(pontos));
    }

    [Fact]
    public void Tempo_ativo_desconta_pausas_longas()
    {
        var instantes = new List<DateTimeOffset>
        {
            Inicio, Inicio.AddMinutes(2), Inicio.AddMinutes(4),     // 4 min trabalhando
            Inicio.AddMinutes(64), Inicio.AddMinutes(66)            // pausa de 1 h, depois mais 2 min
        };

        Assert.Equal(TimeSpan.FromMinutes(6), CalculadoraDeJornada.TempoAtivo(instantes));
    }
}
