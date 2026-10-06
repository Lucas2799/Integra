using System.Globalization;
using System.Text.RegularExpressions;
using Intrega.Modulos.Geocodificacao.Contratos;
using Intrega.Modulos.Paradas.Contratos;
using Intrega.Nucleo.Geo;

namespace Intrega.Modulos.Importacao.Leitura;

/// <summary>Converte a linha de texto da planilha numa <see cref="NovaParada"/>.</summary>
internal static partial class ConversorDeLinhas
{
    [GeneratedRegex(@"\b(\d{5})-?(\d{3})\b")] private static partial Regex CepNoTexto();
    [GeneratedRegex(@"^(\d{1,2})\s*[:hH]\s*(\d{2})?$")] private static partial Regex Horario();

    public static NovaParada Converter(LinhaImportada l)
    {
        var cep = NormalizarCep(l.Cep) ?? (l.EnderecoCompleto is { } texto && CepNoTexto().Match(texto) is { Success: true } m
            ? m.Groups[1].Value + m.Groups[2].Value
            : null);

        var endereco = string.IsNullOrWhiteSpace(l.Logradouro) && !string.IsNullOrWhiteSpace(l.EnderecoCompleto)
            ? new EnderecoInformado(Complemento: l.Complemento, Bairro: l.Bairro, Cidade: l.Cidade, Uf: l.Uf, Cep: cep,
                TextoLivre: l.EnderecoCompleto)
            : new EnderecoInformado(l.Logradouro, l.Numero, l.Complemento, l.Bairro, l.Cidade, l.Uf, cep);

        PontoGeo? local = LerNumero(l.Latitude) is { } lat && LerNumero(l.Longitude) is { } lng &&
                          new PontoGeo(lat, lng) is { Valido: true } ponto
            ? ponto
            : null;

        return new NovaParada(
            endereco,
            l.NomeDoDestinatario,
            l.Telefone,
            l.Observacoes,
            l.CodigoDoPacote is { } codigo ? [codigo] : null,
            1,
            IdentificarMarketplace(l.Marketplace, l.CodigoDoPacote),
            LerHorario(l.JanelaInicio),
            LerHorario(l.JanelaFim),
            Local: local);
    }

    /// <summary>O Excel tira o zero à esquerda do CEP (01310100 → 1310100): completa para 8 dígitos.</summary>
    internal static string? NormalizarCep(string? cep)
    {
        if (string.IsNullOrWhiteSpace(cep)) return null;
        var digitos = new string(cep.Where(char.IsDigit).ToArray());
        return digitos.Length is 7 or 8 ? digitos.PadLeft(8, '0') : null;
    }

    internal static TimeOnly? LerHorario(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        if (TimeOnly.TryParse(texto, CultureInfo.GetCultureInfo("pt-BR"), out var hora)) return hora;
        var m = Horario().Match(texto.Trim());
        if (!m.Success) return null;
        var h = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var min = m.Groups[2].Success ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
        return h is >= 0 and < 24 && min is >= 0 and < 60 ? new TimeOnly(h, min) : null;
    }

    private static double? LerNumero(string? texto) =>
        double.TryParse(texto?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var valor) ? valor : null;

    internal static Marketplace IdentificarMarketplace(string? texto, string? codigo)
    {
        var t = (texto ?? "").ToLowerInvariant();
        if (t.Contains("shopee")) return Marketplace.Shopee;
        if (t.Contains("mercado") || t.Contains("meli") || t == "ml") return Marketplace.MercadoLivre;
        if (t.Contains("amazon")) return Marketplace.Amazon;
        if (t.Contains("magalu") || t.Contains("magazine")) return Marketplace.Magalu;
        if (t.Contains("shein")) return Marketplace.Shein;
        if (t.Contains("aliexpress")) return Marketplace.AliExpress;
        if (t.Contains("correios")) return Marketplace.Correios;
        if (t.Length > 0) return Marketplace.Outro;
        // Sem coluna de marketplace: tenta deduzir pelo formato do código (BR... = Shopee/SPX).
        return codigo is { } c && c.StartsWith("BR", StringComparison.OrdinalIgnoreCase) && c.Length >= 13
            ? Marketplace.Shopee
            : Marketplace.Desconhecido;
    }
}
