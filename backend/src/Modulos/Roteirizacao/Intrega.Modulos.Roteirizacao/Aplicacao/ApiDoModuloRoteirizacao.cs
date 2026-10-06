using Intrega.Modulos.Roteirizacao.Contratos;
using Intrega.Modulos.Roteirizacao.Infraestrutura;
using Microsoft.EntityFrameworkCore;

namespace Intrega.Modulos.Roteirizacao.Aplicacao;

internal sealed class ApiDoModuloRoteirizacao(ContextoRoteirizacao bd) : IModuloRoteirizacao
{
    public async Task<AcessoARota?> ObterAcessoAsync(Guid rotaId, CancellationToken ct = default) =>
        await bd.Rotas.AsNoTracking()
            .Where(r => r.Id == rotaId)
            .Select(r => new AcessoARota(r.Id, r.DonoId, r.EntregadorId, r.OrganizacaoId, r.Status, r.Data,
                new Intrega.Nucleo.Geo.PontoGeo(r.LatitudeDeSaida, r.LongitudeDeSaida)))
            .FirstOrDefaultAsync(ct);
}
