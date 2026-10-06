using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Intrega.Modulos.Geocodificacao.Contratos;
using Intrega.Modulos.Paradas.Contratos;

namespace Intrega.Modulos.Etiquetas.Interpretacao;

/// <summary>
/// Extrai destinatário, endereço e código de rastreio do texto lido por OCR e dos códigos (QR/barras).
///
/// IMPORTANTE: nas etiquetas de marketplace o QR code normalmente traz só o código do envio,
/// não o endereço. Por isso o endereço vem do texto impresso (OCR feito no celular com ML Kit).
/// As regras abaixo são heurísticas genéricas e PRECISAM ser validadas com etiquetas reais
/// (ver testes em Intrega.TestesUnitarios/Etiquetas).
/// </summary>
internal static partial class InterpretadorDeEtiquetas
{
    [GeneratedRegex(@"\b(\d{5})[-\s.]?(\d{3})\b")] private static partial Regex Cep();
    [GeneratedRegex(@"^[A-Z]{2}\d{9}[A-Z]{2}$")] private static partial Regex RastreioCorreios();
    [GeneratedRegex(@"\b[A-Z]{2}\d{9}BR\b")] private static partial Regex RastreioCorreiosNoTexto();
    [GeneratedRegex(@"^BR\d{10,16}[A-Z]?$")] private static partial Regex RastreioShopee();
    [GeneratedRegex(@"\bBR\d{10,16}[A-Z]?\b")] private static partial Regex RastreioShopeeNoTexto();
    [GeneratedRegex(@"^[A-Z0-9-]{8,40}$")] private static partial Regex CodigoGenerico();
    [GeneratedRegex(@"\(?\b(\d{2})\)?\s?(9?\d{4})[-\s]?(\d{4})\b")] private static partial Regex Telefone();
    [GeneratedRegex(@"^(?<logradouro>.+?)[,\s]+(?:n[º°o.]?\s*)?(?<numero>\d{1,6}[A-Za-z]?|s\/?n)\b\s*(?:[-,]\s*(?<complemento>.+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex LinhaDeLogradouro();
    [GeneratedRegex(@"(?<cidade>[A-Za-zÀ-ÿ' .]{3,}?)\s*[-/,]\s*(?<uf>[A-Z]{2})\b")] private static partial Regex CidadeUf();

    private static readonly string[] TiposDeLogradouro =
    [
        "rua", "r", "avenida", "av", "avda", "travessa", "tv", "trav", "alameda", "al", "estrada", "est", "rodovia", "rod",
        "praca", "pca", "largo", "viela", "beco", "servidao", "vila", "quadra", "qd", "conjunto", "via", "caminho", "ladeira"
    ];

    private static readonly HashSet<string> Ufs =
    [
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI",
        "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO"
    ];

    public static EtiquetaInterpretada Interpretar(string? textoLido, IReadOnlyList<CodigoLido>? codigos)
    {
        var avisos = new List<string>();
        var texto = textoLido ?? string.Empty;
        var linhas = texto.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

        var marketplace = IdentificarMarketplace(texto, codigos);
        var rastreio = ExtrairRastreio(codigos, texto, ref marketplace);

        var bloco = BlocoDoDestinatario(linhas, out var nomeNoCabecalho, out var achouMarcador);
        if (!achouMarcador)
            avisos.Add("Não encontrei a palavra \"Destinatário\" na etiqueta; confira se o endereço não é o do remetente.");

        var todosOsCeps = Cep().Matches(texto).Select(m => m.Groups[1].Value + m.Groups[2].Value).Distinct().ToList();
        var cep = bloco.Select(l => Cep().Match(l)).FirstOrDefault(m => m.Success) is { } mc
            ? mc.Groups[1].Value + mc.Groups[2].Value
            : todosOsCeps.LastOrDefault(); // sem bloco claro: o destinatário costuma vir depois do remetente
        if (cep is null) avisos.Add("Nenhum CEP encontrado.");
        else if (todosOsCeps.Count > 1 && !achouMarcador) avisos.Add("Há mais de um CEP na etiqueta; confira o do destinatário.");

        var (logradouro, numero, complemento, indiceDoLogradouro) = ExtrairLogradouro(bloco);
        complemento ??= ValorRotulado(bloco, "complemento", "compl", "apto", "apartamento", "bloco");
        var bairro = ValorRotulado(bloco, "bairro");
        var (cidade, uf) = ExtrairCidadeUf(bloco);

        // Bairro sem rótulo: costuma ser a linha logo depois do logradouro, sem dígitos.
        if (bairro is null && indiceDoLogradouro >= 0 && indiceDoLogradouro + 1 < bloco.Count)
        {
            var candidato = bloco[indiceDoLogradouro + 1];
            if (!candidato.Any(char.IsDigit) && CidadeUf().Match(candidato) is { Success: false } && !TemRotulo(candidato))
                bairro = candidato;
        }

        var nome = nomeNoCabecalho ?? ExtrairNome(bloco, indiceDoLogradouro);
        var telefone = Telefone().Matches(string.Join('\n', bloco))
            .Select(m => $"{m.Groups[1].Value}{m.Groups[2].Value}{m.Groups[3].Value}")
            .FirstOrDefault(t => cep is null || !t.Contains(cep, StringComparison.Ordinal));

        var confianca = (cep is not null ? 0.35 : 0) + (logradouro is not null ? 0.25 : 0) + (numero is not null ? 0.15 : 0) +
                        (cidade is not null ? 0.1 : 0) + (nome is not null ? 0.1 : 0) + (rastreio is not null ? 0.05 : 0);
        if (!achouMarcador) confianca = Math.Min(confianca, 0.7);

        var endereco = new EnderecoInformado(logradouro, numero, complemento, bairro, cidade, uf, cep,
            logradouro is null && bloco.Count > 0 ? string.Join(", ", bloco.Take(4)) : null);

        return new EtiquetaInterpretada(marketplace, rastreio, nome, telefone, endereco, todosOsCeps,
            Math.Round(confianca, 2), avisos);
    }

    internal static Marketplace IdentificarMarketplace(string texto, IReadOnlyList<CodigoLido>? codigos)
    {
        var t = SemAcentos(texto).ToLowerInvariant();
        if (t.Contains("shopee") || t.Contains("spx")) return Marketplace.Shopee;
        if (t.Contains("mercado livre") || t.Contains("mercadolivre") || t.Contains("mercado envios")) return Marketplace.MercadoLivre;
        if (t.Contains("amazon")) return Marketplace.Amazon;
        if (t.Contains("magalu") || t.Contains("magazine luiza")) return Marketplace.Magalu;
        if (t.Contains("shein")) return Marketplace.Shein;
        if (t.Contains("aliexpress") || t.Contains("cainiao")) return Marketplace.AliExpress;
        if (t.Contains("correios") || t.Contains("sedex")) return Marketplace.Correios;
        // QR do Mercado Livre costuma ser um JSON com "sender_id".
        if (codigos?.Any(c => c.Valor.Contains("sender_id", StringComparison.OrdinalIgnoreCase)) == true)
            return Marketplace.MercadoLivre;
        return Marketplace.Desconhecido;
    }

    internal static string? ExtrairRastreio(IReadOnlyList<CodigoLido>? codigos, string texto, ref Marketplace marketplace)
    {
        foreach (var codigo in codigos ?? [])
        {
            var valor = codigo.Valor.Trim();
            if (valor.StartsWith('{') && IdDoJson(valor) is { } id) return id;
            var maiusculo = valor.ToUpperInvariant();
            if (RastreioShopee().IsMatch(maiusculo))
            {
                if (marketplace == Marketplace.Desconhecido) marketplace = Marketplace.Shopee;
                return maiusculo;
            }
            if (RastreioCorreios().IsMatch(maiusculo))
            {
                if (marketplace == Marketplace.Desconhecido) marketplace = Marketplace.Correios;
                return maiusculo;
            }
        }

        // Nenhum padrão conhecido: usa o primeiro código "com cara" de identificador.
        var generico = codigos?.Select(c => c.Valor.Trim().ToUpperInvariant()).FirstOrDefault(v => CodigoGenerico().IsMatch(v));
        if (generico is not null) return generico;

        var maiusculas = texto.ToUpperInvariant();
        if (RastreioShopeeNoTexto().Match(maiusculas) is { Success: true } s) return s.Value;
        if (RastreioCorreiosNoTexto().Match(maiusculas) is { Success: true } c) return c.Value;
        return null;
    }

    private static string? IdDoJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var chave in new[] { "id", "shipment_id", "tracking", "tracking_number", "codigo" })
            {
                if (doc.RootElement.TryGetProperty(chave, out var v))
                    return v.ValueKind == JsonValueKind.Number ? v.GetRawText() : v.GetString();
            }
        }
        catch (JsonException)
        {
        }
        return null;
    }

    /// <summary>Linhas que pertencem ao destinatário: depois de "Destinatário" e antes de "Remetente".</summary>
    private static List<string> BlocoDoDestinatario(List<string> linhas, out string? nomeNoCabecalho, out bool achouMarcador)
    {
        nomeNoCabecalho = null;
        var simplificadas = linhas.Select(l => SemAcentos(l).ToUpperInvariant()).ToList();
        var inicio = simplificadas.FindIndex(l => l.Contains("DESTINATARI") || l.StartsWith("PARA:", StringComparison.Ordinal));
        achouMarcador = inicio >= 0;

        if (!achouMarcador)
        {
            // Sem marcador: remove o bloco do remetente (se houver) e usa o resto.
            var remetente = simplificadas.FindIndex(l => l.Contains("REMETENTE"));
            if (remetente < 0) return linhas;
            var fimDoRemetente = Math.Min(linhas.Count, remetente + 6);
            return linhas.Where((_, i) => i < remetente || i >= fimDoRemetente).ToList();
        }

        // "Destinatário: Maria Silva" na mesma linha.
        var cabecalho = linhas[inicio];
        var doisPontos = cabecalho.IndexOf(':');
        if (doisPontos >= 0 && doisPontos < cabecalho.Length - 1)
        {
            var depois = cabecalho[(doisPontos + 1)..].Trim();
            if (depois.Length >= 3 && !depois.Any(char.IsDigit)) nomeNoCabecalho = Capitalizar(depois);
        }

        var fim = simplificadas.FindIndex(inicio + 1, l => l.Contains("REMETENTE") || l.StartsWith("DE:", StringComparison.Ordinal));
        return linhas.Skip(inicio + 1).Take((fim < 0 ? linhas.Count : fim) - inicio - 1).ToList();
    }

    private static (string? Logradouro, string? Numero, string? Complemento, int Indice) ExtrairLogradouro(List<string> bloco)
    {
        for (var i = 0; i < bloco.Count; i++)
        {
            var linha = RemoverRotulo(bloco[i], "endereco", "end", "logradouro");
            var primeiraPalavra = SemAcentos(linha).ToLowerInvariant().Split([' ', '.', ','], StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            if (primeiraPalavra is null || !TiposDeLogradouro.Contains(primeiraPalavra)) continue;

            var m = LinhaDeLogradouro().Match(linha);
            if (m.Success)
            {
                var numero = m.Groups["numero"].Value;
                return (m.Groups["logradouro"].Value.Trim(' ', ','),
                    numero.Replace("/", "", StringComparison.Ordinal).Equals("sn", StringComparison.OrdinalIgnoreCase) ? "S/N" : numero,
                    m.Groups["complemento"].Success ? m.Groups["complemento"].Value.Trim() : null, i);
            }
            return (linha.Trim(' ', ','), null, null, i);
        }
        return (null, null, null, -1);
    }

    private static (string? Cidade, string? Uf) ExtrairCidadeUf(List<string> bloco)
    {
        foreach (var linha in bloco)
        {
            // Tira o CEP da linha ("01310-100 São Paulo - SP").
            var semCep = Cep().Replace(linha, " ").Replace("CEP", " ", StringComparison.OrdinalIgnoreCase).Trim(' ', '-', ',', ':');
            foreach (Match m in CidadeUf().Matches(semCep))
            {
                var uf = m.Groups["uf"].Value;
                var cidade = m.Groups["cidade"].Value.Trim(' ', '-', ',', '.');
                if (!Ufs.Contains(uf) || cidade.Length < 3) continue;
                if (TiposDeLogradouro.Contains(SemAcentos(cidade).ToLowerInvariant().Split(' ')[0])) continue;
                return (Capitalizar(cidade), uf);
            }
        }
        return (null, null);
    }

    private static string? ExtrairNome(List<string> bloco, int indiceDoLogradouro)
    {
        var limite = indiceDoLogradouro >= 0 ? indiceDoLogradouro : Math.Min(bloco.Count, 2);
        for (var i = 0; i < limite; i++)
        {
            var linha = RemoverRotulo(bloco[i], "nome");
            if (linha.Length >= 3 && !linha.Any(char.IsDigit) && !TemRotulo(linha) && linha.Contains(' '))
                return Capitalizar(linha);
        }
        return null;
    }

    private static string? ValorRotulado(List<string> bloco, params string[] rotulos)
    {
        foreach (var linha in bloco)
        {
            var simples = SemAcentos(linha).ToLowerInvariant();
            foreach (var rotulo in rotulos)
            {
                if (!simples.StartsWith(rotulo, StringComparison.Ordinal)) continue;
                var resto = linha[Math.Min(linha.Length, rotulo.Length)..].TrimStart(':', '.', ' ', '-');
                if (resto.Length > 0) return resto.Trim();
            }
        }
        return null;
    }

    private static bool TemRotulo(string linha) =>
        SemAcentos(linha).ToLowerInvariant() is var s &&
        (s.StartsWith("cep", StringComparison.Ordinal) || s.StartsWith("tel", StringComparison.Ordinal) ||
         s.StartsWith("pedido", StringComparison.Ordinal) || s.StartsWith("nf", StringComparison.Ordinal) ||
         s.StartsWith("bairro", StringComparison.Ordinal) || s.StartsWith("compl", StringComparison.Ordinal));

    private static string RemoverRotulo(string linha, params string[] rotulos)
    {
        var simples = SemAcentos(linha).ToLowerInvariant();
        foreach (var rotulo in rotulos)
        {
            if (simples.StartsWith(rotulo + ":", StringComparison.Ordinal) || simples.StartsWith(rotulo + " :", StringComparison.Ordinal))
                return linha[(linha.IndexOf(':') + 1)..].Trim();
        }
        return linha;
    }

    internal static string SemAcentos(string texto)
    {
        var decomposto = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);
        foreach (var c in decomposto)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>OCR costuma devolver tudo em maiúsculas: "MARIA DA SILVA" vira "Maria da Silva".</summary>
    private static string Capitalizar(string texto)
    {
        var minusculas = new HashSet<string> { "da", "de", "do", "das", "dos", "e" };
        var palavras = texto.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select((p, i) => i > 0 && minusculas.Contains(p) ? p : char.ToUpperInvariant(p[0]) + p[1..]);
        return string.Join(' ', palavras);
    }
}
