using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Intrega.Modulos.Geocodificacao.Contratos;

namespace Intrega.Modulos.Geocodificacao.Aplicacao;

/// <summary>Normaliza endereços brasileiros para a chave do cache e para consultar os geocodificadores.</summary>
internal static partial class NormalizadorDeEndereco
{
    private static readonly (Regex Padrao, string Substituto)[] Abreviacoes =
    [
        (Prefixo("r"), "rua "),
        (Prefixo("av|avd|aven"), "avenida "),
        (Prefixo("al"), "alameda "),
        (Prefixo("tv|trav"), "travessa "),
        (Prefixo("est|estr"), "estrada "),
        (Prefixo("rod"), "rodovia "),
        (Prefixo("pc|pca|pç|pça"), "praca "),
        (Prefixo("lgo|lg"), "largo "),
        (Prefixo("vl"), "vila "),
        (Prefixo("jd|jdm"), "jardim "),
        (Prefixo("pq"), "parque "),
        (Prefixo("cond"), "condominio "),
        (Prefixo("dr"), "doutor "),
        (Prefixo("prof|profa"), "professor "),
        (Prefixo("cel"), "coronel "),
        (Prefixo("gal|gen"), "general "),
        (Prefixo("sto|sta"), "santo "),
        (Prefixo("eng"), "engenheiro ")
    ];

    private static Regex Prefixo(string alternativas) =>
        new($@"\b(?:{alternativas})\.?\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [GeneratedRegex(@"\D")] private static partial Regex NaoDigitos();
    [GeneratedRegex(@"[^a-z0-9 ]")] private static partial Regex NaoAlfanumericos();
    [GeneratedRegex(@"\s+")] private static partial Regex Espacos();
    [GeneratedRegex(@"\b\d{5}-?\d{3}\b")] private static partial Regex CepNoTexto();
    [GeneratedRegex(@"(?:,\s*|\bn[º°o.]\s*)(\d{1,5}[A-Za-z]?)(?=$|[,\s-])", RegexOptions.IgnoreCase)] private static partial Regex NumeroIsolado();

    /// <summary>
    /// Número depois de vírgula ou "nº", ignorando o CEP ("Rua Augusta, 900, SP" → "900").
    /// Exigir a vírgula evita confundir com nomes como "Rua 7 de Setembro".
    /// </summary>
    public static string? NumeroNoTexto(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var m = NumeroIsolado().Match(CepNoTexto().Replace(texto, " "));
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>Minúsculas, sem acentos, sem pontuação e com espaços únicos.</summary>
    public static string Simplificar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return string.Empty;
        var decomposto = valor.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);
        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        }
        var semAcentos = sb.ToString().Normalize(NormalizationForm.FormC);
        return Espacos().Replace(NaoAlfanumericos().Replace(semAcentos, " "), " ").Trim();
    }

    public static string ExpandirAbreviacoes(string valor)
    {
        var resultado = valor;
        foreach (var (padrao, substituto) in Abreviacoes)
            resultado = padrao.Replace(resultado, substituto);
        return resultado.Trim();
    }

    /// <summary>Devolve só os 8 dígitos do CEP, ou null se não for um CEP válido.</summary>
    public static string? NormalizarCep(string? cep)
    {
        if (string.IsNullOrWhiteSpace(cep)) return null;
        var digitos = NaoDigitos().Replace(cep, "");
        return digitos.Length == 8 ? digitos : null;
    }

    /// <summary>Chave estável do cache: o mesmo endereço escrito de jeitos diferentes gera a mesma chave.</summary>
    public static string MontarChave(EnderecoInformado e)
    {
        var cep = NormalizarCep(e.Cep);
        if (!string.IsNullOrWhiteSpace(e.Logradouro))
        {
            var logradouro = Simplificar(ExpandirAbreviacoes(e.Logradouro));
            return $"l:{logradouro}|n:{Simplificar(e.Numero)}|c:{Simplificar(e.Cidade)}|cep:{cep}";
        }
        return $"t:{Simplificar(ExpandirAbreviacoes(e.TextoLivre ?? string.Empty))}|cep:{cep}";
    }

    public static string MontarDescricao(string? logradouro, string? numero, string? complemento, string? bairro,
        string? cidade, string? uf)
    {
        var linha1 = string.Join(", ", new[] { logradouro, numero }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (!string.IsNullOrWhiteSpace(complemento)) linha1 += $" - {complemento}";
        var cidadeUf = string.Join("/", new[] { cidade, uf }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return string.Join(" - ", new[] { linha1, bairro, cidadeUf }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    public static EnderecoNormalizado Normalizar(EnderecoInformado e)
    {
        var uf = UfsBrasileiras.ParaUf(e.Uf);
        var descricao = MontarDescricao(e.Logradouro, e.Numero, e.Complemento, e.Bairro, e.Cidade, uf);
        return new EnderecoNormalizado(e.Logradouro?.Trim(), e.Numero?.Trim(), e.Complemento?.Trim(), e.Bairro?.Trim(),
            e.Cidade?.Trim(), uf, NormalizarCep(e.Cep),
            descricao.Length > 0 ? descricao : e.TextoLivre?.Trim() ?? e.Cep ?? "Endereço sem descrição");
    }
}
