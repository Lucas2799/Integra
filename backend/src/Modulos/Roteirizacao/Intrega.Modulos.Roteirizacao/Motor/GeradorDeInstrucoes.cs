namespace Intrega.Modulos.Roteirizacao.Motor;

/// <summary>
/// Gera instruções de navegação em português a partir das manobras do OSRM (texto e voz).
/// Os códigos de manobra ("turn", "roundabout"...) são do protocolo OSRM e por isso ficam em inglês.
/// </summary>
internal static class GeradorDeInstrucoes
{
    // Vias "masculinas" usam "no/pelo"; as demais (Rua, Avenida, Estrada...) usam "na/pela".
    private static readonly string[] PrefixosMasculinos =
    [
        "viaduto", "largo", "beco", "acesso", "anel", "elevado", "corredor", "caminho", "túnel", "tunel",
        "contorno", "parque", "trevo", "boulevard", "bulevar", "eixo", "ramal", "retorno", "complexo", "conjunto"
    ];

    public static string Gerar(string manobra, string? modificador, string? via, int? saida)
    {
        var temVia = !string.IsNullOrWhiteSpace(via);
        return manobra switch
        {
            "depart" => temVia ? $"Siga {Pela(via!)}" : "Siga em frente",
            "arrive" => modificador switch
            {
                "right" or "slight right" or "sharp right" => "Você chegou. O destino está à direita",
                "left" or "slight left" or "sharp left" => "Você chegou. O destino está à esquerda",
                _ => "Você chegou ao destino"
            },
            "roundabout" or "rotary" => saida is { } s
                ? $"Na rotatória, pegue a {s}ª saída" + (temVia ? $" para {via}" : "")
                : "Entre na rotatória" + (temVia ? $" e siga para {via}" : ""),
            "exit roundabout" or "exit rotary" => "Saia da rotatória" + (temVia ? $" {Pela(via!)}" : ""),
            "roundabout turn" => $"Na rotatória, {Direcao(modificador).ToLowerInvariant()}",
            "new name" => temVia ? $"Continue {Pela(via!)}" : "Continue em frente",
            "continue" => modificador is null or "straight"
                ? (temVia ? $"Continue {Pela(via!)}" : "Continue em frente")
                : $"{Direcao(modificador)}{Na(via)}",
            "merge" => temVia ? $"Entre{Na(via)}" : "Entre na via",
            "on ramp" => $"Pegue o acesso {Lado(modificador)}" + (temVia ? $" para {via}" : ""),
            "off ramp" => $"Pegue a saída {Lado(modificador)}" + (temVia ? $" para {via}" : ""),
            "fork" => $"Na bifurcação, mantenha-se {Lado(modificador)}" + (temVia ? $" para {via}" : ""),
            "end of road" => $"No fim da via, {Direcao(modificador).ToLowerInvariant()}{Na(via)}",
            "turn" => $"{Direcao(modificador)}{Na(via)}",
            _ => temVia ? $"Continue {Pela(via!)}" : "Continue em frente"
        };
    }

    private static string Direcao(string? modificador) => modificador switch
    {
        "uturn" => "Faça o retorno",
        "sharp right" => "Vire acentuadamente à direita",
        "right" => "Vire à direita",
        "slight right" => "Mantenha-se à direita",
        "straight" => "Siga em frente",
        "slight left" => "Mantenha-se à esquerda",
        "left" => "Vire à esquerda",
        "sharp left" => "Vire acentuadamente à esquerda",
        _ => "Continue"
    };

    private static string Lado(string? modificador) =>
        modificador?.Contains("left", StringComparison.Ordinal) == true ? "à esquerda" : "à direita";

    private static bool Masculino(string via) =>
        PrefixosMasculinos.Any(p => via.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    private static string Na(string? via) =>
        string.IsNullOrWhiteSpace(via) ? "" : Masculino(via) ? $" no {via}" : $" na {via}";

    private static string Pela(string via) => Masculino(via) ? $"pelo {via}" : $"pela {via}";
}
