namespace Intrega.Modulos.Geocodificacao.Dominio;

/// <summary>Cache de geocodificação. Correções manuais (CorrecaoManual) têm prioridade e não expiram.</summary>
internal sealed class EnderecoEmCache
{
    private EnderecoEmCache() { }

    public string Chave { get; private set; } = default!;
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public double Confianca { get; private set; }
    public string Provedor { get; private set; } = default!;
    public string? Logradouro { get; private set; }
    public string? Numero { get; private set; }
    public string? Bairro { get; private set; }
    public string? Cidade { get; private set; }
    public string? Uf { get; private set; }
    public string? Cep { get; private set; }
    public string Descricao { get; private set; } = default!;
    public bool CorrecaoManual { get; private set; }
    public int Acessos { get; private set; }
    public DateTimeOffset AtualizadoEm { get; private set; } = DateTimeOffset.UtcNow;

    public static EnderecoEmCache Criar(string chave) => new() { Chave = chave };

    public void Atualizar(double latitude, double longitude, double confianca, string provedor, string? logradouro,
        string? numero, string? bairro, string? cidade, string? uf, string? cep, string descricao, bool correcaoManual)
    {
        Latitude = latitude;
        Longitude = longitude;
        Confianca = confianca;
        Provedor = provedor;
        Logradouro = logradouro;
        Numero = numero;
        Bairro = bairro;
        Cidade = cidade;
        Uf = uf;
        Cep = cep;
        Descricao = descricao;
        CorrecaoManual = correcaoManual;
        AtualizadoEm = DateTimeOffset.UtcNow;
    }

    public void RegistrarAcesso() => Acessos++;
}

internal sealed class CepEmCache
{
    private CepEmCache() { }

    public string Cep { get; private set; } = default!;
    public string? Logradouro { get; private set; }
    public string? Bairro { get; private set; }
    public string Cidade { get; private set; } = default!;
    public string Uf { get; private set; } = default!;
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public string Provedor { get; private set; } = default!;
    public DateTimeOffset AtualizadoEm { get; private set; } = DateTimeOffset.UtcNow;

    public static CepEmCache Criar(string cep, string? logradouro, string? bairro, string cidade, string uf,
        double? latitude, double? longitude, string provedor) => new()
    {
        Cep = cep, Logradouro = logradouro, Bairro = bairro, Cidade = cidade, Uf = uf,
        Latitude = latitude, Longitude = longitude, Provedor = provedor
    };
}
