using Intrega.Modulos.Geocodificacao.Contratos;
using Intrega.Modulos.Paradas.Contratos;
using Intrega.Nucleo.Dominio;
using Intrega.Nucleo.Geo;

namespace Intrega.Modulos.Paradas.Dominio;

/// <summary>Destino de entrega. Pode agrupar vários pacotes no mesmo endereço.</summary>
internal sealed class Parada : Entidade
{
    public const int TempoDeAtendimentoPadraoSegundos = 120;

    private Parada() { }

    public Guid ResponsavelId { get; private set; }
    public Guid? OrganizacaoId { get; private set; }
    public Guid? RotaId { get; private set; }

    public EnderecoDaParada Endereco { get; private set; } = default!;
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public double? ConfiancaDaLocalizacao { get; private set; }
    public string? ProvedorDaLocalizacao { get; private set; }
    public bool LocalConfirmado { get; private set; }

    public string? NomeDoDestinatario { get; private set; }
    public string? TelefoneDoDestinatario { get; private set; }
    public string? Observacoes { get; private set; }
    public List<string> CodigosDePacote { get; private set; } = [];
    public int QuantidadeDePacotes { get; private set; } = 1;
    public Marketplace Marketplace { get; private set; }
    public OrigemDaParada Origem { get; private set; }

    public TimeOnly? JanelaInicio { get; private set; }
    public TimeOnly? JanelaFim { get; private set; }
    public int Prioridade { get; private set; }
    public int TempoDeAtendimentoSegundos { get; private set; } = TempoDeAtendimentoPadraoSegundos;

    public StatusDaParada Status { get; private set; } = StatusDaParada.Pendente;
    public int Tentativas { get; private set; }
    public MotivoDaFalha? UltimoMotivoDeFalha { get; private set; }
    public EstrategiaDeNovaTentativa? EstrategiaDeNovaTentativa { get; private set; }
    public DateTimeOffset? NovaTentativaApos { get; private set; }

    public DateTimeOffset? EntregueEm { get; private set; }
    public double? LatitudeDaEntrega { get; private set; }
    public double? LongitudeDaEntrega { get; private set; }
    public string? RecebidoPor { get; private set; }
    public string? ObservacoesDaEntrega { get; private set; }
    public string? CaminhoDoComprovante { get; private set; }

    public PontoGeo? Local => Latitude is { } lat && Longitude is { } lng ? new PontoGeo(lat, lng) : null;

    /// <summary>Sem coordenada ou com baixa confiança: o app pede para o entregador conferir o pino.</summary>
    public bool PrecisaConfirmarLocal =>
        !LocalConfirmado && (Local is null || ConfiancaDaLocalizacao < ResultadoDeGeocodificacao.LimiteParaConfirmacao);

    public static Parada Criar(ResponsavelPelaParada responsavel, Guid? rotaId, NovaParada dados, OrigemDaParada origem)
    {
        var parada = new Parada
        {
            ResponsavelId = responsavel.UsuarioId,
            OrganizacaoId = responsavel.OrganizacaoId,
            RotaId = rotaId,
            Origem = origem
        };
        parada.AtualizarDados(dados);
        return parada;
    }

    public void AtualizarDados(NovaParada dados)
    {
        NomeDoDestinatario = Limpar(dados.NomeDoDestinatario, 120);
        TelefoneDoDestinatario = Limpar(dados.TelefoneDoDestinatario, 30);
        Observacoes = Limpar(dados.Observacoes, 500);
        CodigosDePacote = (dados.CodigosDePacote ?? []).Select(c => c.Trim()).Where(c => c.Length > 0).Distinct().ToList();
        QuantidadeDePacotes = Math.Max(1, Math.Max(dados.QuantidadeDePacotes, CodigosDePacote.Count));
        Marketplace = dados.Marketplace;
        JanelaInicio = dados.JanelaInicio;
        JanelaFim = dados.JanelaFim;
        Prioridade = Math.Clamp(dados.Prioridade, 0, 10);
        TempoDeAtendimentoSegundos = Math.Clamp(dados.TempoDeAtendimentoSegundos ?? TempoDeAtendimentoPadraoSegundos, 15, 3600);
        MarcarAlteracao();
    }

    public void DefinirEndereco(EnderecoNormalizado endereco) => Endereco = EnderecoDaParada.De(endereco);

    public void DefinirLocalizacao(PontoGeo local, double confianca, string provedor)
    {
        Latitude = local.Latitude;
        Longitude = local.Longitude;
        ConfiancaDaLocalizacao = confianca;
        ProvedorDaLocalizacao = provedor;
        LocalConfirmado = false;
        MarcarAlteracao();
    }

    /// <summary>O entregador conferiu/arrastou o pino para o lugar certo.</summary>
    public void ConfirmarLocal(PontoGeo local)
    {
        Latitude = local.Latitude;
        Longitude = local.Longitude;
        ConfiancaDaLocalizacao = 1;
        ProvedorDaLocalizacao = "manual";
        LocalConfirmado = true;
        MarcarAlteracao();
    }

    /// <summary>Junta outra parada do mesmo endereço nesta (ex.: dois pacotes para o mesmo prédio).</summary>
    public void Agrupar(Parada outra)
    {
        foreach (var codigo in outra.CodigosDePacote.Where(c => !CodigosDePacote.Contains(c)))
            CodigosDePacote.Add(codigo);
        QuantidadeDePacotes = Math.Max(QuantidadeDePacotes + outra.QuantidadeDePacotes, CodigosDePacote.Count);
        if (outra.Observacoes is not null && Observacoes?.Contains(outra.Observacoes) != true)
            Observacoes = Limpar(string.Join(" | ", new[] { Observacoes, outra.Observacoes }.Where(o => o is not null)), 500);
        MarcarAlteracao();
    }

    public void AdicionarCodigoDePacote(string codigo)
    {
        if (CodigosDePacote.Contains(codigo)) return;
        CodigosDePacote.Add(codigo);
        QuantidadeDePacotes = Math.Max(QuantidadeDePacotes, CodigosDePacote.Count);
        MarcarAlteracao();
    }

    public void AtribuirARota(Guid? rotaId)
    {
        RotaId = rotaId;
        MarcarAlteracao();
    }

    public void MarcarEntregue(DateTimeOffset em, PontoGeo? local, string? recebidoPor, string? observacoes)
    {
        Status = StatusDaParada.Entregue;
        EntregueEm = em;
        LatitudeDaEntrega = local?.Latitude;
        LongitudeDaEntrega = local?.Longitude;
        RecebidoPor = Limpar(recebidoPor, 120);
        ObservacoesDaEntrega = Limpar(observacoes, 500);
        NovaTentativaApos = null;
        MarcarAlteracao();
    }

    /// <summary>Registra a tentativa sem sucesso e aplica a estratégia escolhida pelo entregador.</summary>
    public void RegistrarFalha(MotivoDaFalha motivo, EstrategiaDeNovaTentativa estrategia, DateTimeOffset agora, int minutosParaNovaTentativa)
    {
        Tentativas++;
        UltimoMotivoDeFalha = motivo;
        EstrategiaDeNovaTentativa = estrategia;
        switch (estrategia)
        {
            case Contratos.EstrategiaDeNovaTentativa.FimDaRota:
                Status = StatusDaParada.Pendente;
                NovaTentativaApos = null;
                break;
            case Contratos.EstrategiaDeNovaTentativa.AposMinutos:
                Status = StatusDaParada.Pendente;
                NovaTentativaApos = agora.AddMinutes(Math.Clamp(minutosParaNovaTentativa, 5, 600));
                break;
            case Contratos.EstrategiaDeNovaTentativa.ProximoDia:
                Status = StatusDaParada.Adiada;
                RotaId = null;
                NovaTentativaApos = null;
                break;
            case Contratos.EstrategiaDeNovaTentativa.DevolverAoRemetente:
                Status = StatusDaParada.Devolvida;
                NovaTentativaApos = null;
                break;
        }
        MarcarAlteracao();
    }

    /// <summary>Desfaz entrega/falha (toque errado do entregador).</summary>
    public void Reabrir()
    {
        Status = StatusDaParada.Pendente;
        EntregueEm = null;
        LatitudeDaEntrega = null;
        LongitudeDaEntrega = null;
        RecebidoPor = null;
        NovaTentativaApos = null;
        EstrategiaDeNovaTentativa = null;
        MarcarAlteracao();
    }

    public void DefinirComprovante(string caminho)
    {
        CaminhoDoComprovante = caminho;
        MarcarAlteracao();
    }

    private static string? Limpar(string? valor, int tamanhoMaximo)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var texto = valor.Trim();
        return texto.Length <= tamanhoMaximo ? texto : texto[..tamanhoMaximo];
    }
}

internal sealed record EnderecoDaParada(
    string? Logradouro,
    string? Numero,
    string? Complemento,
    string? Bairro,
    string? Cidade,
    string? Uf,
    string? Cep,
    string Descricao)
{
    public static EnderecoDaParada De(EnderecoNormalizado e) =>
        new(e.Logradouro, e.Numero, e.Complemento, e.Bairro, e.Cidade, e.Uf, e.Cep, e.Descricao);

    public EnderecoNormalizado ParaContrato() => new(Logradouro, Numero, Complemento, Bairro, Cidade, Uf, Cep, Descricao);

    public EnderecoInformado ParaInformado() =>
        new(Logradouro, Numero, Complemento, Bairro, Cidade, Uf, Cep, string.IsNullOrEmpty(Logradouro) ? Descricao : null);
}
