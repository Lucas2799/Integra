using Intrega.Modulos.Identidade.Contratos;
using Microsoft.Extensions.Options;

namespace Intrega.Modulos.Identidade.Infraestrutura;

internal sealed record CompraVerificada(Plano Plano, DateTimeOffset ExpiraEm);

/// <summary>
/// Valida a compra junto à loja. Implementações futuras: Google Play Developer API,
/// App Store Server API ou RevenueCat (via webhook). A interface isola essa escolha do resto do sistema.
/// </summary>
internal interface IVerificadorDeCompraNaLoja
{
    Task<CompraVerificada?> VerificarAsync(string plataforma, string produtoId, string comprovante, CancellationToken ct);
}

/// <summary>Verificador de desenvolvimento: aceita comprovantes "dev-*" quando Assinatura:PermitirComprasDeTeste=true.</summary>
internal sealed class VerificadorDeCompraDeTeste(IOptions<OpcoesDeAssinatura> opcoes, TimeProvider relogio)
    : IVerificadorDeCompraNaLoja
{
    public Task<CompraVerificada?> VerificarAsync(string plataforma, string produtoId, string comprovante, CancellationToken ct)
    {
        if (!opcoes.Value.PermitirComprasDeTeste || !comprovante.StartsWith("dev-", StringComparison.Ordinal))
            return Task.FromResult<CompraVerificada?>(null);

        var produto = CatalogoDeProdutos.Buscar(produtoId);
        return Task.FromResult(produto is null
            ? null
            : new CompraVerificada(produto.Plano, relogio.GetUtcNow().AddDays(produto.DiasDeVigencia)));
    }
}

internal sealed record ProdutoDaLoja(string Id, Plano Plano, int DiasDeVigencia, decimal PrecoEmReais, string Descricao);

/// <summary>Produtos cadastrados na Play Store/App Store (os IDs precisam ser iguais nas lojas).</summary>
internal static class CatalogoDeProdutos
{
    public static readonly IReadOnlyList<ProdutoDaLoja> Todos =
    [
        new("intrega_pro_mensal", Plano.Pro, 31, 19.90m, "Intrega Pro mensal"),
        new("intrega_pro_anual", Plano.Pro, 366, 179.90m, "Intrega Pro anual"),
        new("intrega_frota_por_entregador_mensal", Plano.Frota, 31, 29.90m, "Intrega Frota, por entregador/mês")
    ];

    public static ProdutoDaLoja? Buscar(string id) => Todos.FirstOrDefault(p => p.Id == id);
}
