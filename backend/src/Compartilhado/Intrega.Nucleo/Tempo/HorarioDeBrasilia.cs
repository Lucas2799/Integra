namespace Intrega.Nucleo.Tempo;

/// <summary>Datas de rota, janelas de entrega e limites diários usam o horário de Brasília, não UTC.</summary>
public static class HorarioDeBrasilia
{
    public static readonly TimeZoneInfo Fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public static DateTimeOffset ParaLocal(DateTimeOffset utc) => TimeZoneInfo.ConvertTime(utc, Fuso);

    public static DateOnly Hoje(TimeProvider relogio) => DateOnly.FromDateTime(ParaLocal(relogio.GetUtcNow()).DateTime);

    /// <summary>Converte data + hora locais de Brasília em um instante absoluto.</summary>
    public static DateTimeOffset Em(DateOnly data, TimeOnly hora)
    {
        var local = data.ToDateTime(hora, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, Fuso.GetUtcOffset(local));
    }
}
