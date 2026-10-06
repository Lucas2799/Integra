using Intrega.Modulos.Paradas.Contratos;

namespace Intrega.Modulos.Rastreamento.Dominio;

/// <summary>Posição GPS enviada pelo app (em lote, inclusive quando estava sem internet).</summary>
internal sealed class AmostraDePosicao
{
    private AmostraDePosicao() { }

    public long Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public Guid? RotaId { get; private set; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public double? VelocidadeMetrosPorSegundo { get; private set; }
    public double? Direcao { get; private set; }
    public double? PrecisaoMetros { get; private set; }
    public DateTimeOffset RegistradaEm { get; private set; }

    public static AmostraDePosicao Criar(Guid usuarioId, Guid? rotaId, double latitude, double longitude,
        double? velocidade, double? direcao, double? precisao, DateTimeOffset registradaEm) => new()
    {
        UsuarioId = usuarioId,
        RotaId = rotaId,
        Latitude = latitude,
        Longitude = longitude,
        VelocidadeMetrosPorSegundo = velocidade,
        Direcao = direcao,
        PrecisaoMetros = precisao,
        RegistradaEm = registradaEm
    };
}

public enum ResultadoDaTentativa
{
    Entregue,
    NaoEntregue
}

/// <summary>Histórico de cada tentativa de entrega: base dos relatórios de produtividade e ganhos.</summary>
internal sealed class RegistroDeEntrega
{
    private RegistroDeEntrega() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid UsuarioId { get; private set; }
    public Guid? RotaId { get; private set; }
    public Guid ParadaId { get; private set; }
    public ResultadoDaTentativa Resultado { get; private set; }
    public MotivoDaFalha? Motivo { get; private set; }
    public Marketplace Marketplace { get; private set; }
    public int QuantidadeDePacotes { get; private set; }
    public DateTimeOffset OcorridoEm { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }

    public static RegistroDeEntrega Entregue(ParadaEntregue e) => new()
    {
        UsuarioId = e.UsuarioId,
        RotaId = e.RotaId,
        ParadaId = e.ParadaId,
        Resultado = ResultadoDaTentativa.Entregue,
        Marketplace = e.Marketplace,
        QuantidadeDePacotes = e.QuantidadeDePacotes,
        OcorridoEm = e.EntregueEm,
        Latitude = e.Local?.Latitude,
        Longitude = e.Local?.Longitude
    };

    public static RegistroDeEntrega NaoEntregue(EntregaNaoRealizada e) => new()
    {
        UsuarioId = e.UsuarioId,
        RotaId = e.RotaId,
        ParadaId = e.ParadaId,
        Resultado = ResultadoDaTentativa.NaoEntregue,
        Motivo = e.Motivo,
        OcorridoEm = e.OcorridoEm,
        Latitude = e.LocalAtual?.Latitude,
        Longitude = e.LocalAtual?.Longitude
    };
}

/// <summary>Quanto o entregador ganha e gasta, para o painel financeiro.</summary>
internal sealed class ConfiguracaoFinanceira
{
    private ConfiguracaoFinanceira() { }

    public Guid UsuarioId { get; private set; }
    public decimal? ValorPorEntrega { get; private set; }
    public decimal? ValorPorPacote { get; private set; }
    public decimal? PrecoDoCombustivelPorLitro { get; private set; }
    public decimal? KmPorLitro { get; private set; }
    public decimal? CustoFixoPorDia { get; private set; }

    public static ConfiguracaoFinanceira Nova(Guid usuarioId) => new() { UsuarioId = usuarioId };

    public void Atualizar(decimal? valorPorEntrega, decimal? valorPorPacote, decimal? precoDoCombustivel,
        decimal? kmPorLitro, decimal? custoFixoPorDia)
    {
        ValorPorEntrega = valorPorEntrega;
        ValorPorPacote = valorPorPacote;
        PrecoDoCombustivelPorLitro = precoDoCombustivel;
        KmPorLitro = kmPorLitro;
        CustoFixoPorDia = custoFixoPorDia;
    }
}
