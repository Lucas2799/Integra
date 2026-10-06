namespace Intrega.Modulos.Importacao.Leitura;

/// <summary>Linha da planilha já mapeada para os campos do Intrega (valores ainda como texto).</summary>
public sealed record LinhaImportada
{
    public int NumeroDaLinha { get; init; }
    public string? Logradouro { get; init; }
    public string? Numero { get; init; }
    public string? Complemento { get; init; }
    public string? Bairro { get; init; }
    public string? Cidade { get; init; }
    public string? Uf { get; init; }
    public string? Cep { get; init; }
    public string? EnderecoCompleto { get; init; }
    public string? NomeDoDestinatario { get; init; }
    public string? Telefone { get; init; }
    public string? Observacoes { get; init; }
    public string? CodigoDoPacote { get; init; }
    public string? Marketplace { get; init; }
    public string? JanelaInicio { get; init; }
    public string? JanelaFim { get; init; }
    public string? Latitude { get; init; }
    public string? Longitude { get; init; }

    public bool TemEndereco =>
        !string.IsNullOrWhiteSpace(Logradouro) || !string.IsNullOrWhiteSpace(EnderecoCompleto) ||
        !string.IsNullOrWhiteSpace(Cep) ||
        (!string.IsNullOrWhiteSpace(Latitude) && !string.IsNullOrWhiteSpace(Longitude));
}

public enum CampoDaPlanilha
{
    Logradouro, Numero, Complemento, Bairro, Cidade, Uf, Cep, EnderecoCompleto,
    NomeDoDestinatario, Telefone, Observacoes, CodigoDoPacote, Marketplace, JanelaInicio, JanelaFim, Latitude, Longitude
}

/// <summary>Resultado da leitura: linhas, como cada coluna foi entendida e o que ficou de fora.</summary>
public sealed record PlanilhaLida(
    IReadOnlyList<LinhaImportada> Linhas,
    IReadOnlyDictionary<string, CampoDaPlanilha> ColunasReconhecidas,
    IReadOnlyList<string> ColunasIgnoradas,
    IReadOnlyList<string> Avisos);
