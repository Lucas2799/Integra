using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;

namespace Intrega.Modulos.Importacao.Leitura;

/// <summary>Lê CSV (separador ; , ou tab, em UTF-8 ou Latin-1) e XLSX (primeira aba).</summary>
internal static class LeitorDePlanilha
{
    public const int MaximoDeLinhas = 2000;

    public static PlanilhaLida Ler(Stream conteudo, string nomeDoArquivo)
    {
        var extensao = Path.GetExtension(nomeDoArquivo).ToLowerInvariant();
        var tabela = extensao switch
        {
            ".xlsx" or ".xlsm" => LerXlsx(conteudo),
            ".csv" or ".txt" => LerCsv(conteudo),
            _ => throw new NotSupportedException("Formato não suportado. Envie um arquivo .xlsx ou .csv.")
        };
        return Mapear(tabela);
    }

    internal static PlanilhaLida Mapear(List<string[]> tabela)
    {
        var avisos = new List<string>();
        // O cabeçalho é a primeira linha com pelo menos 2 células preenchidas (pula títulos e linhas vazias).
        var indiceDoCabecalho = tabela.FindIndex(l => l.Count(c => !string.IsNullOrWhiteSpace(c)) >= 2);
        if (indiceDoCabecalho < 0)
            return new PlanilhaLida([], new Dictionary<string, CampoDaPlanilha>(), [], ["Cabeçalho não encontrado."]);

        var cabecalhos = tabela[indiceDoCabecalho];
        var mapa = ReconhecedorDeColunas.Reconhecer(cabecalhos);
        var colunaDoCampo = mapa.ToDictionary(kv => kv.Value, kv => kv.Key);
        if (!mapa.Values.Any(c => c is CampoDaPlanilha.Logradouro or CampoDaPlanilha.EnderecoCompleto
                                     or CampoDaPlanilha.Cep or CampoDaPlanilha.Latitude))
            avisos.Add("Nenhuma coluna de endereço reconhecida (ex.: endereço, rua, CEP).");

        var linhas = new List<LinhaImportada>();
        for (var l = indiceDoCabecalho + 1; l < tabela.Count; l++)
        {
            var celulas = tabela[l];
            if (celulas.All(string.IsNullOrWhiteSpace)) continue;
            if (linhas.Count >= MaximoDeLinhas)
            {
                avisos.Add($"A planilha tem mais de {MaximoDeLinhas} linhas; só as primeiras foram consideradas.");
                break;
            }

            string? Valor(CampoDaPlanilha campo)
            {
                if (!colunaDoCampo.TryGetValue(campo, out var coluna) || coluna >= celulas.Length) return null;
                var texto = celulas[coluna]?.Trim();
                return string.IsNullOrEmpty(texto) ? null : texto;
            }

            linhas.Add(new LinhaImportada
            {
                NumeroDaLinha = l + 1,
                Logradouro = Valor(CampoDaPlanilha.Logradouro),
                Numero = Valor(CampoDaPlanilha.Numero),
                Complemento = Valor(CampoDaPlanilha.Complemento),
                Bairro = Valor(CampoDaPlanilha.Bairro),
                Cidade = Valor(CampoDaPlanilha.Cidade),
                Uf = Valor(CampoDaPlanilha.Uf),
                Cep = Valor(CampoDaPlanilha.Cep),
                EnderecoCompleto = Valor(CampoDaPlanilha.EnderecoCompleto),
                NomeDoDestinatario = Valor(CampoDaPlanilha.NomeDoDestinatario),
                Telefone = Valor(CampoDaPlanilha.Telefone),
                Observacoes = Valor(CampoDaPlanilha.Observacoes),
                CodigoDoPacote = Valor(CampoDaPlanilha.CodigoDoPacote),
                Marketplace = Valor(CampoDaPlanilha.Marketplace),
                JanelaInicio = Valor(CampoDaPlanilha.JanelaInicio),
                JanelaFim = Valor(CampoDaPlanilha.JanelaFim),
                Latitude = Valor(CampoDaPlanilha.Latitude),
                Longitude = Valor(CampoDaPlanilha.Longitude)
            });
        }

        var reconhecidas = mapa.ToDictionary(kv => cabecalhos[kv.Key], kv => kv.Value);
        var ignoradas = cabecalhos.Where((c, i) => !mapa.ContainsKey(i) && !string.IsNullOrWhiteSpace(c)).ToList();
        return new PlanilhaLida(linhas, reconhecidas, ignoradas, avisos);
    }

    private static List<string[]> LerXlsx(Stream conteudo)
    {
        using var pasta = new XLWorkbook(conteudo);
        var aba = pasta.Worksheets.First();
        var area = aba.RangeUsed();
        if (area is null) return [];
        var colunas = area.ColumnCount();
        return area.Rows()
            .Take(MaximoDeLinhas + 20)
            .Select(linha => Enumerable.Range(1, colunas).Select(c => TextoDaCelula(linha.Cell(c))).ToArray())
            .ToList();
    }

    private static string TextoDaCelula(IXLCell celula) => celula.DataType switch
    {
        // Números saem sem formatação (o CEP sem zero à esquerda é corrigido na conversão).
        XLDataType.Number => celula.GetDouble().ToString("0.##########", CultureInfo.InvariantCulture),
        XLDataType.TimeSpan => celula.GetTimeSpan().ToString(@"hh\:mm"),
        XLDataType.DateTime => celula.GetDateTime().ToString("HH:mm"),
        _ => celula.GetFormattedString()
    };

    private static List<string[]> LerCsv(Stream conteudo)
    {
        using var memoria = new MemoryStream();
        conteudo.CopyTo(memoria);
        var texto = DecodificarTexto(memoria.ToArray());

        var primeiraLinha = texto.Split('\n', 2)[0];
        var separador = primeiraLinha.Count(c => c == ';') >= primeiraLinha.Count(c => c == ',') ? ";" : ",";
        if (primeiraLinha.Count(c => c == '\t') > primeiraLinha.Count(c => c == separador[0])) separador = "\t";

        var configuracao = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = separador,
            HasHeaderRecord = false,
            BadDataFound = null,
            MissingFieldFound = null,
            TrimOptions = TrimOptions.Trim
        };
        using var leitor = new StringReader(texto);
        using var csv = new CsvReader(leitor, configuracao);
        var linhas = new List<string[]>();
        while (csv.Read() && linhas.Count < MaximoDeLinhas + 20)
        {
            linhas.Add(csv.Parser.Record ?? []);
        }
        return linhas;
    }

    /// <summary>UTF-8 quando válido; senão Latin-1 (o Excel em português costuma salvar CSV assim).</summary>
    internal static string DecodificarTexto(byte[] bytes)
    {
        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes).TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }
}
