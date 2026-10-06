using Intrega.Modulos.Identidade.Contratos;
using Intrega.Modulos.Importacao.Dominio;
using Intrega.Modulos.Importacao.Infraestrutura;
using Intrega.Modulos.Importacao.Leitura;
using Intrega.Modulos.Roteirizacao.Contratos;
using Intrega.Nucleo.Autenticacao;
using Intrega.Nucleo.Resultados;
using Microsoft.EntityFrameworkCore;

namespace Intrega.Modulos.Importacao.Aplicacao;

public sealed record PreVisualizacaoDto(
    int TotalDeLinhas,
    IReadOnlyDictionary<string, CampoDaPlanilha> ColunasReconhecidas,
    IReadOnlyList<string> ColunasIgnoradas,
    IReadOnlyList<string> Avisos,
    IReadOnlyList<LinhaImportada> PrimeirasLinhas);

public sealed record ErroDeLinhaDto(int Linha, string Mensagem);

public sealed record ImportacaoDto(
    Guid Id,
    string NomeDoArquivo,
    Guid? RotaId,
    StatusDaImportacao Status,
    int TotalDeLinhas,
    int LinhasProcessadas,
    int ParadasCriadas,
    int PrecisamConfirmacao,
    int SemLocalizacao,
    IReadOnlyList<ErroDeLinhaDto> Erros,
    DateTimeOffset CriadoEm,
    DateTimeOffset? ConcluidaEm);

internal sealed class ServicoDeImportacao(
    ContextoImportacao bd,
    FilaDeImportacao fila,
    IModuloIdentidade identidade,
    IModuloRoteirizacao roteirizacao,
    IUsuarioAtual usuario)
{
    public const long TamanhoMaximoDoArquivo = 5 * 1024 * 1024;

    /// <summary>Lê a planilha e mostra como as colunas foram entendidas, sem criar nada.</summary>
    public Resultado<PreVisualizacaoDto> PreVisualizar(Stream conteudo, string nomeDoArquivo)
    {
        var leitura = Ler(conteudo, nomeDoArquivo);
        if (leitura.Falha) return leitura.Erro!;
        var planilha = leitura.Valor;
        return new PreVisualizacaoDto(planilha.Linhas.Count, planilha.ColunasReconhecidas, planilha.ColunasIgnoradas,
            planilha.Avisos, planilha.Linhas.Take(10).ToList());
    }

    public async Task<Resultado<ImportacaoDto>> ImportarAsync(Stream conteudo, string nomeDoArquivo, Guid? rotaId, CancellationToken ct)
    {
        var limites = (await identidade.ObterDireitosAsync(usuario.Id, ct)).Limites;
        if (!limites.ImportarPlanilha)
            return Erro.LimiteDoPlano("plano.importar_planilha", "A importação de planilhas está disponível no plano Pro.");

        Guid? organizacaoId = usuario.OrganizacaoId;
        if (rotaId is { } id)
        {
            var rota = await roteirizacao.ObterAcessoAsync(id, ct);
            if (rota is null || !rota.PodeSerAcessadaPor(usuario.Id, usuario.OrganizacaoId, usuario.GestorDaOrganizacao))
                return Erro.NaoEncontrado("rota.nao_encontrada", "Rota não encontrada.");
            organizacaoId = rota.OrganizacaoId;
        }

        var leitura = Ler(conteudo, nomeDoArquivo);
        if (leitura.Falha) return leitura.Erro!;
        if (leitura.Valor.Linhas.Count == 0)
            return Erro.Validacao("importacao.vazia", string.Join(" ", leitura.Valor.Avisos.DefaultIfEmpty("A planilha não tem linhas.")));

        var trabalho = TrabalhoDeImportacao.Criar(usuario.Id, organizacaoId, rotaId, nomeDoArquivo, leitura.Valor.Linhas);
        bd.Trabalhos.Add(trabalho);
        await bd.SaveChangesAsync(ct);
        await fila.EnfileirarAsync(trabalho.Id, ct);
        return ParaDto(trabalho);
    }

    public async Task<Resultado<ImportacaoDto>> ObterAsync(Guid id, CancellationToken ct)
    {
        var trabalho = await bd.Trabalhos.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id && t.UsuarioId == usuario.Id, ct);
        return trabalho is null
            ? Erro.NaoEncontrado("importacao.nao_encontrada", "Importação não encontrada.")
            : ParaDto(trabalho);
    }

    public async Task<IReadOnlyList<ImportacaoDto>> ListarRecentesAsync(CancellationToken ct) =>
        (await bd.Trabalhos.AsNoTracking()
            .Where(t => t.UsuarioId == usuario.Id)
            .OrderByDescending(t => t.CriadoEm)
            .Take(20)
            .ToListAsync(ct))
        .Select(ParaDto).ToList();

    /// <summary>Modelo de planilha para o usuário baixar e preencher.</summary>
    public static string ModeloCsv() =>
        "destinatario;telefone;cep;rua;numero;complemento;bairro;cidade;uf;codigo_rastreio;marketplace;observacao\n" +
        "Maria Silva;11999998888;01310-100;Avenida Paulista;1578;Apto 12;Bela Vista;São Paulo;SP;BR2412345678901;Shopee;Portão azul\n";

    private static Resultado<PlanilhaLida> Ler(Stream conteudo, string nomeDoArquivo)
    {
        try
        {
            return LeitorDePlanilha.Ler(conteudo, nomeDoArquivo);
        }
        catch (NotSupportedException ex)
        {
            return Erro.Validacao("importacao.formato", ex.Message);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or FormatException or ArgumentException)
        {
            return Erro.Validacao("importacao.arquivo_invalido", "Não foi possível ler o arquivo. Verifique se é um .xlsx ou .csv válido.");
        }
    }

    private static ImportacaoDto ParaDto(TrabalhoDeImportacao t) => new(
        t.Id, t.NomeDoArquivo, t.RotaId, t.Status, t.TotalDeLinhas, t.LinhasProcessadas, t.ParadasCriadas,
        t.PrecisamConfirmacao, t.SemLocalizacao, t.Erros.Select(e => new ErroDeLinhaDto(e.Linha, e.Mensagem)).ToList(),
        t.CriadoEm, t.ConcluidaEm);
}
