namespace Intrega.Modulos.Geocodificacao.Aplicacao;

internal static class UfsBrasileiras
{
    private static readonly Dictionary<string, string> NomePorUf = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AC"] = "Acre", ["AL"] = "Alagoas", ["AP"] = "Amapá", ["AM"] = "Amazonas", ["BA"] = "Bahia",
        ["CE"] = "Ceará", ["DF"] = "Distrito Federal", ["ES"] = "Espírito Santo", ["GO"] = "Goiás",
        ["MA"] = "Maranhão", ["MT"] = "Mato Grosso", ["MS"] = "Mato Grosso do Sul", ["MG"] = "Minas Gerais",
        ["PA"] = "Pará", ["PB"] = "Paraíba", ["PR"] = "Paraná", ["PE"] = "Pernambuco", ["PI"] = "Piauí",
        ["RJ"] = "Rio de Janeiro", ["RN"] = "Rio Grande do Norte", ["RS"] = "Rio Grande do Sul",
        ["RO"] = "Rondônia", ["RR"] = "Roraima", ["SC"] = "Santa Catarina", ["SP"] = "São Paulo",
        ["SE"] = "Sergipe", ["TO"] = "Tocantins"
    };

    public static bool EhUf(string? valor) => valor is { Length: 2 } && NomePorUf.ContainsKey(valor);

    public static string? NomeDoEstado(string? uf) => uf is not null && NomePorUf.TryGetValue(uf, out var nome) ? nome : uf;

    /// <summary>Converte "São Paulo", "sao paulo", "SP" ou "BR-SP" para "SP".</summary>
    public static string? ParaUf(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var v = valor.Trim();
        if (v.StartsWith("BR-", StringComparison.OrdinalIgnoreCase)) v = v[3..];
        if (EhUf(v)) return v.ToUpperInvariant();
        var chave = NormalizadorDeEndereco.Simplificar(v);
        return NomePorUf.FirstOrDefault(kv => NormalizadorDeEndereco.Simplificar(kv.Value) == chave).Key ?? v;
    }
}
