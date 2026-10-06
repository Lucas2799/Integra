namespace Intrega.Modulos.Roteirizacao.Motor;

/// <summary>Seção "Roteirizacao" do appsettings.</summary>
public sealed class OpcoesDeRoteirizacao
{
    public const string Secao = "Roteirizacao";

    /// <summary>"Osrm" ou "LinhaReta" (estimativa sem serviço externo; útil em testes e sem internet).</summary>
    public string Motor { get; set; } = "Osrm";

    public OpcoesOsrm Osrm { get; set; } = new();

    /// <summary>"OrTools" ou "Heuristica".</summary>
    public string Otimizador { get; set; } = "OrTools";

    public int TempoLimiteDoOtimizadorSegundos { get; set; } = 5;
}

public sealed class OpcoesOsrm
{
    // Padrão: servidores públicos da FOSSGIS (uso justo, custo zero).
    // Em produção, aponte para o OSRM próprio (ver infra/osrm).
    public string UrlCarro { get; set; } = "https://routing.openstreetmap.de/routed-car/";
    /// <summary>Vazio = usa o perfil de carro com <see cref="FatorDeTempoMoto"/>.</summary>
    public string UrlMoto { get; set; } = "";
    public string UrlBicicleta { get; set; } = "https://routing.openstreetmap.de/routed-bike/";
    public string UrlAPe { get; set; } = "https://routing.openstreetmap.de/routed-foot/";
    /// <summary>Moto costuma ser mais rápida que carro no trânsito urbano.</summary>
    public double FatorDeTempoMoto { get; set; } = 0.85;
    /// <summary>Máximo de origens/destinos por chamada de /table (max-table-size do servidor).</summary>
    public int TamanhoMaximoDaMatriz { get; set; } = 100;
    public int MaximoDePontosPorTrajeto { get; set; } = 90;
    public int TempoLimiteSegundos { get; set; } = 20;
}
