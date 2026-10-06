using System.Threading.Channels;

namespace Intrega.Modulos.Importacao.Infraestrutura;

/// <summary>Fila em memória de importações a processar (custo zero; trocável por um broker no futuro).</summary>
internal sealed class FilaDeImportacao
{
    private readonly Channel<Guid> _canal = Channel.CreateUnbounded<Guid>();

    public ValueTask EnfileirarAsync(Guid trabalhoId, CancellationToken ct = default) => _canal.Writer.WriteAsync(trabalhoId, ct);

    public IAsyncEnumerable<Guid> LerTodosAsync(CancellationToken ct) => _canal.Reader.ReadAllAsync(ct);
}
