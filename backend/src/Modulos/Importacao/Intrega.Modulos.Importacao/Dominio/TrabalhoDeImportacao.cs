using Intrega.Modulos.Importacao.Leitura;
using Intrega.Nucleo.Dominio;

namespace Intrega.Modulos.Importacao.Dominio;

public enum StatusDaImportacao
{
    NaFila,
    Processando,
    Concluida,
    Falhou
}

/// <summary>
/// Uma importação de planilha. As linhas ficam salvas até o fim do processamento,
/// assim uma importação interrompida (reinício do servidor) é retomada.
/// </summary>
internal sealed class TrabalhoDeImportacao : Entidade
{
    private TrabalhoDeImportacao() { }

    public Guid UsuarioId { get; private set; }
    public Guid? OrganizacaoId { get; private set; }
    public Guid? RotaId { get; private set; }
    public string NomeDoArquivo { get; private set; } = default!;
    public StatusDaImportacao Status { get; private set; } = StatusDaImportacao.NaFila;
    public int TotalDeLinhas { get; private set; }
    public int LinhasProcessadas { get; private set; }
    public int ParadasCriadas { get; private set; }
    public int PrecisamConfirmacao { get; private set; }
    public int SemLocalizacao { get; private set; }
    public List<ErroDeLinha> Erros { get; private set; } = [];
    public List<LinhaImportada> LinhasPendentes { get; private set; } = [];
    public DateTimeOffset? ConcluidaEm { get; private set; }

    public static TrabalhoDeImportacao Criar(Guid usuarioId, Guid? organizacaoId, Guid? rotaId, string nomeDoArquivo,
        IReadOnlyList<LinhaImportada> linhas) => new()
    {
        UsuarioId = usuarioId,
        OrganizacaoId = organizacaoId,
        RotaId = rotaId,
        NomeDoArquivo = nomeDoArquivo,
        TotalDeLinhas = linhas.Count,
        LinhasPendentes = linhas.ToList()
    };

    public void Iniciar()
    {
        Status = StatusDaImportacao.Processando;
        MarcarAlteracao();
    }

    /// <summary>Registra um lote processado e remove essas linhas das pendentes.</summary>
    public void RegistrarLote(int linhasDoLote, int criadas, int precisamConfirmacao, int semLocalizacao, IEnumerable<ErroDeLinha> erros)
    {
        LinhasPendentes = LinhasPendentes.Skip(linhasDoLote).ToList();
        LinhasProcessadas += linhasDoLote;
        ParadasCriadas += criadas;
        PrecisamConfirmacao += precisamConfirmacao;
        SemLocalizacao += semLocalizacao;
        Erros.AddRange(erros);
        MarcarAlteracao();
    }

    public void Concluir(DateTimeOffset agora)
    {
        Status = StatusDaImportacao.Concluida;
        LinhasPendentes = [];
        ConcluidaEm = agora;
        MarcarAlteracao();
    }

    public void Falhar(string mensagem, DateTimeOffset agora)
    {
        Status = StatusDaImportacao.Falhou;
        Erros.Add(new ErroDeLinha(0, mensagem));
        ConcluidaEm = agora;
        MarcarAlteracao();
    }
}

internal sealed class ErroDeLinha
{
    public ErroDeLinha() { }

    public ErroDeLinha(int linha, string mensagem)
    {
        Linha = linha;
        Mensagem = mensagem;
    }

    public int Linha { get; set; }
    public string Mensagem { get; set; } = "";
}
