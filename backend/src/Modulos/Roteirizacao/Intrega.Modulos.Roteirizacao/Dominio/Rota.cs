using Intrega.Modulos.Roteirizacao.Contratos;
using Intrega.Nucleo.Dominio;
using Intrega.Nucleo.Geo;

namespace Intrega.Modulos.Roteirizacao.Dominio;

/// <summary>Rota de um entregador em um dia. Guarda a sequência otimizada e o tempo de cada trecho.</summary>
internal sealed class Rota : Entidade
{
    private Rota() { }

    public Guid DonoId { get; private set; }
    public Guid EntregadorId { get; private set; }
    public Guid? OrganizacaoId { get; private set; }

    public string Nome { get; private set; } = default!;
    public DateOnly Data { get; private set; }
    public StatusDaRota Status { get; private set; } = StatusDaRota.Rascunho;
    public PerfilDeVeiculo Veiculo { get; private set; }

    public double LatitudeDeSaida { get; private set; }
    public double LongitudeDeSaida { get; private set; }
    public string? DescricaoDaSaida { get; private set; }
    public double? LatitudeDeChegada { get; private set; }
    public double? LongitudeDeChegada { get; private set; }
    public string? DescricaoDaChegada { get; private set; }
    public bool VoltarAoInicio { get; private set; }
    public DateTimeOffset? HorarioPlanejadoDeSaida { get; private set; }

    public DateTimeOffset? IniciadaEm { get; private set; }
    public DateTimeOffset? ConcluidaEm { get; private set; }
    public DateTimeOffset? OtimizadaEm { get; private set; }
    public DateTimeOffset? SaidaConsiderada { get; private set; }
    public string? AlgoritmoUsado { get; private set; }
    public string? MotorUsado { get; private set; }

    public double DistanciaTotalMetros { get; private set; }
    public double DuracaoTotalSegundos { get; private set; }
    /// <summary>Traçado completo da rota (polyline codificada) para desenhar no mapa.</summary>
    public string? Geometria { get; private set; }
    public int Recalculos { get; private set; }
    public uint Versao { get; private set; }

    public List<ItemDaSequencia> Sequencia { get; private set; } = [];

    public PontoGeo Saida => new(LatitudeDeSaida, LongitudeDeSaida);

    /// <summary>Destino final: ponto de chegada, volta ao início ou nenhum (rota aberta).</summary>
    public PontoGeo? Chegada => VoltarAoInicio ? Saida
        : LatitudeDeChegada is { } lat && LongitudeDeChegada is { } lng ? new PontoGeo(lat, lng) : null;

    public static Rota Criar(Guid donoId, Guid entregadorId, Guid? organizacaoId, string nome, DateOnly data,
        PerfilDeVeiculo veiculo, PontoGeo saida, string? descricaoDaSaida, PontoGeo? chegada, string? descricaoDaChegada,
        bool voltarAoInicio, DateTimeOffset? horarioPlanejadoDeSaida)
    {
        var rota = new Rota { DonoId = donoId, EntregadorId = entregadorId, OrganizacaoId = organizacaoId, Data = data };
        rota.AlterarConfiguracao(nome, veiculo, saida, descricaoDaSaida, chegada, descricaoDaChegada, voltarAoInicio,
            horarioPlanejadoDeSaida);
        return rota;
    }

    public void AlterarConfiguracao(string nome, PerfilDeVeiculo veiculo, PontoGeo saida, string? descricaoDaSaida,
        PontoGeo? chegada, string? descricaoDaChegada, bool voltarAoInicio, DateTimeOffset? horarioPlanejadoDeSaida)
    {
        Nome = nome.Trim();
        Veiculo = veiculo;
        LatitudeDeSaida = saida.Latitude;
        LongitudeDeSaida = saida.Longitude;
        DescricaoDaSaida = descricaoDaSaida?.Trim();
        VoltarAoInicio = voltarAoInicio;
        LatitudeDeChegada = voltarAoInicio ? null : chegada?.Latitude;
        LongitudeDeChegada = voltarAoInicio ? null : chegada?.Longitude;
        DescricaoDaChegada = voltarAoInicio ? null : descricaoDaChegada?.Trim();
        HorarioPlanejadoDeSaida = horarioPlanejadoDeSaida;
        MarcarAlteracao();
    }

    public void AplicarPlano(IReadOnlyList<ItemDaSequencia> sequencia, double distanciaMetros, double duracaoSegundos,
        string geometria, DateTimeOffset saidaConsiderada, string algoritmo, string motor, DateTimeOffset agora)
    {
        Sequencia = sequencia.ToList();
        DistanciaTotalMetros = distanciaMetros;
        DuracaoTotalSegundos = duracaoSegundos;
        Geometria = geometria;
        SaidaConsiderada = saidaConsiderada;
        OtimizadaEm = agora;
        AlgoritmoUsado = algoritmo;
        MotorUsado = motor;
        if (Status == StatusDaRota.Rascunho) Status = StatusDaRota.Planejada;
        MarcarAlteracao();
    }

    public void RegistrarRecalculo() => Recalculos++;

    public void Iniciar(DateTimeOffset agora)
    {
        if (Status is StatusDaRota.EmAndamento or StatusDaRota.Concluida) return;
        Status = StatusDaRota.EmAndamento;
        IniciadaEm = agora;
        MarcarAlteracao();
    }

    public void Concluir(DateTimeOffset agora)
    {
        Status = StatusDaRota.Concluida;
        ConcluidaEm = agora;
        IniciadaEm ??= agora;
        MarcarAlteracao();
    }

    public AcessoARota ParaAcesso() => new(Id, DonoId, EntregadorId, OrganizacaoId, Status, Data, Saida);
}

/// <summary>Posição de uma parada na rota e o trecho que leva até ela.</summary>
internal sealed class ItemDaSequencia
{
    public Guid ParadaId { get; set; }
    public int Ordem { get; set; }
    public double DistanciaDoTrechoMetros { get; set; }
    public double DuracaoDoTrechoSegundos { get; set; }
    /// <summary>Segundos desde a saída até chegar na parada (inclui atendimentos e esperas anteriores).</summary>
    public double ChegadaAposSaidaSegundos { get; set; }
    public DateTimeOffset PrevisaoDeChegada { get; set; }
    public int AtendimentoSegundos { get; set; }
    /// <summary>A chegada prevista passa do fim da janela de horário.</summary>
    public bool AtrasadaParaJanela { get; set; }
}
