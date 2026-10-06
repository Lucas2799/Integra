using System.Text;

namespace Intrega.Nucleo.Geo;

/// <summary>
/// Algoritmo "Encoded Polyline" (Google), precisão 5. É o mesmo formato que o OSRM devolve
/// e reduz muito o tamanho do traçado enviado ao app.
/// </summary>
public static class Polilinha
{
    public static string Codificar(IEnumerable<PontoGeo> pontos, int precisao = 5)
    {
        var fator = Math.Pow(10, precisao);
        var sb = new StringBuilder();
        long latAnterior = 0, lngAnterior = 0;
        foreach (var p in pontos)
        {
            var lat = (long)Math.Round(p.Latitude * fator);
            var lng = (long)Math.Round(p.Longitude * fator);
            CodificarValor(lat - latAnterior, sb);
            CodificarValor(lng - lngAnterior, sb);
            latAnterior = lat;
            lngAnterior = lng;
        }
        return sb.ToString();
    }

    public static List<PontoGeo> Decodificar(string codificada, int precisao = 5)
    {
        var fator = Math.Pow(10, precisao);
        var resultado = new List<PontoGeo>();
        int indice = 0;
        long lat = 0, lng = 0;
        while (indice < codificada.Length)
        {
            lat += DecodificarValor(codificada, ref indice);
            lng += DecodificarValor(codificada, ref indice);
            resultado.Add(new PontoGeo(lat / fator, lng / fator));
        }
        return resultado;
    }

    private static void CodificarValor(long valor, StringBuilder sb)
    {
        valor = valor < 0 ? ~(valor << 1) : valor << 1;
        while (valor >= 0x20)
        {
            sb.Append((char)((0x20 | (valor & 0x1f)) + 63));
            valor >>= 5;
        }
        sb.Append((char)(valor + 63));
    }

    private static long DecodificarValor(string codificada, ref int indice)
    {
        long resultado = 0;
        int deslocamento = 0, b;
        do
        {
            b = codificada[indice++] - 63;
            resultado |= (long)(b & 0x1f) << deslocamento;
            deslocamento += 5;
        } while (b >= 0x20);
        return (resultado & 1) != 0 ? ~(resultado >> 1) : resultado >> 1;
    }
}
