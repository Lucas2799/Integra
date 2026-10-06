using Microsoft.Extensions.Configuration;

namespace Intrega.Infraestrutura.Armazenamento;

/// <summary>Armazenamento de arquivos (fotos de comprovante). Disco local hoje; S3/R2/Azure Blob depois.</summary>
public interface IArmazenamentoDeArquivos
{
    Task SalvarAsync(string caminho, Stream conteudo, CancellationToken ct);
    Task<Stream?> AbrirLeituraAsync(string caminho, CancellationToken ct);
    Task ExcluirAsync(string caminho, CancellationToken ct);
}

public sealed class ArmazenamentoEmDiscoLocal(IConfiguration configuracao) : IArmazenamentoDeArquivos
{
    private readonly string _raiz = Path.GetFullPath(configuracao["Armazenamento:PastaLocal"] ?? "dados/arquivos");

    public async Task SalvarAsync(string caminho, Stream conteudo, CancellationToken ct)
    {
        var completo = Resolver(caminho);
        Directory.CreateDirectory(Path.GetDirectoryName(completo)!);
        await using var arquivo = File.Create(completo);
        await conteudo.CopyToAsync(arquivo, ct);
    }

    public Task<Stream?> AbrirLeituraAsync(string caminho, CancellationToken ct)
    {
        var completo = Resolver(caminho);
        return Task.FromResult<Stream?>(File.Exists(completo) ? File.OpenRead(completo) : null);
    }

    public Task ExcluirAsync(string caminho, CancellationToken ct)
    {
        var completo = Resolver(caminho);
        if (File.Exists(completo)) File.Delete(completo);
        else if (Directory.Exists(completo)) Directory.Delete(completo, recursive: true);
        return Task.CompletedTask;
    }

    /// <summary>Impede que um caminho malicioso ("../") saia da pasta de armazenamento.</summary>
    private string Resolver(string caminho)
    {
        var completo = Path.GetFullPath(Path.Combine(_raiz, caminho));
        if (!completo.StartsWith(_raiz, StringComparison.Ordinal))
            throw new InvalidOperationException("Caminho fora da pasta de armazenamento.");
        return completo;
    }
}
