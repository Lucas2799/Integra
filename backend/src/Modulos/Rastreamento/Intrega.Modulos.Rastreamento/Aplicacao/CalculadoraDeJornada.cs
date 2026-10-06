using Intrega.Nucleo.Geo;

namespace Intrega.Modulos.Rastreamento.Aplicacao;

internal sealed record PontoNoTempo(PontoGeo Local, DateTimeOffset Em);

/// <summary>Contabiliza distância e tempo de trabalho a partir das posições GPS, filtrando ruído do sinal.</summary>
internal static class CalculadoraDeJornada
{
    /// <summary>Intervalo maior que isso entre duas posições é considerado pausa (não soma distância nem tempo).</summary>
    public static readonly TimeSpan IntervaloMaximo = TimeSpan.FromMinutes(5);

    /// <summary>Saltos menores são "tremidas" do GPS parado.</summary>
    public const double DeslocamentoMinimoMetros = 15;

    /// <summary>Velocidade acima disso entre dois pontos indica erro de GPS.</summary>
    public const double VelocidadeMaximaMetrosPorSegundo = 150 / 3.6;

    public static double DistanciaPercorridaMetros(IReadOnlyList<PontoNoTempo> pontos)
    {
        double total = 0;
        PontoNoTempo? ultimoValido = null;
        foreach (var ponto in pontos.OrderBy(p => p.Em))
        {
            if (ultimoValido is null)
            {
                ultimoValido = ponto;
                continue;
            }
            var intervalo = ponto.Em - ultimoValido.Em;
            var distancia = ultimoValido.Local.DistanciaAte(ponto.Local);
            if (intervalo > IntervaloMaximo)
            {
                ultimoValido = ponto; // retomada após pausa
                continue;
            }
            if (distancia < DeslocamentoMinimoMetros) continue;
            if (intervalo.TotalSeconds > 0 && distancia / intervalo.TotalSeconds > VelocidadeMaximaMetrosPorSegundo) continue;
            total += distancia;
            ultimoValido = ponto;
        }
        return total;
    }

    /// <summary>Soma os períodos com posições próximas no tempo (ignora pausas longas, como o almoço).</summary>
    public static TimeSpan TempoAtivo(IReadOnlyList<DateTimeOffset> instantes)
    {
        var ordenados = instantes.Order().ToList();
        var total = TimeSpan.Zero;
        for (var i = 1; i < ordenados.Count; i++)
        {
            var intervalo = ordenados[i] - ordenados[i - 1];
            if (intervalo <= IntervaloMaximo) total += intervalo;
        }
        return total;
    }
}
