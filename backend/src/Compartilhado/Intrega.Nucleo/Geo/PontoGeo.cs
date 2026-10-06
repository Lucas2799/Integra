namespace Intrega.Nucleo.Geo;

/// <summary>Coordenada WGS84. Mantida simples (sem PostGIS) para facilitar a troca de provedores.</summary>
public readonly record struct PontoGeo(double Latitude, double Longitude)
{
    private const double RaioDaTerraEmMetros = 6_371_000;

    public bool Valido =>
        Latitude is >= -90 and <= 90 && Longitude is >= -180 and <= 180 && !(Latitude == 0 && Longitude == 0);

    /// <summary>Distância em linha reta (fórmula de Haversine), em metros.</summary>
    public double DistanciaAte(PontoGeo outro)
    {
        var dLat = EmRadianos(outro.Latitude - Latitude);
        var dLon = EmRadianos(outro.Longitude - Longitude);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(EmRadianos(Latitude)) * Math.Cos(EmRadianos(outro.Latitude)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return RaioDaTerraEmMetros * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double EmRadianos(double graus) => graus * Math.PI / 180;

    public override string ToString() =>
        FormattableString.Invariant($"{Latitude:F6},{Longitude:F6}");
}
