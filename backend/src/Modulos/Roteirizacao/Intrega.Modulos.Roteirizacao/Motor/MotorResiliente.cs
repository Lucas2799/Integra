using Intrega.Modulos.Roteirizacao.Contratos;
using Intrega.Nucleo.Geo;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Intrega.Modulos.Roteirizacao.Motor;

/// <summary>Usa o OSRM quando disponível; em qualquer falha cai para a estimativa em linha reta. O app nunca fica sem rota.</summary>
internal sealed class MotorResiliente(
    MotorOsrm osrm,
    MotorLinhaReta linhaReta,
    IOptions<OpcoesDeRoteirizacao> opcoes,
    ILogger<MotorResiliente> log) : IMotorDeRotas
{
    private bool UsarOsrm(PerfilDeVeiculo perfil) =>
        opcoes.Value.Motor.Equals("Osrm", StringComparison.OrdinalIgnoreCase) && osrm.UrlDoPerfil(perfil) is not null;

    public async Task<MatrizDeDeslocamento> ObterMatrizAsync(IReadOnlyList<PontoGeo> pontos, PerfilDeVeiculo perfil, CancellationToken ct)
    {
        if (UsarOsrm(perfil))
        {
            try
            {
                return await osrm.ObterMatrizAsync(pontos, perfil, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                log.LogWarning(ex, "OSRM indisponível para a matriz; usando estimativa em linha reta");
            }
        }
        return await linhaReta.ObterMatrizAsync(pontos, perfil, ct);
    }

    public async Task<Trajeto> ObterTrajetoAsync(IReadOnlyList<PontoGeo> pontos, PerfilDeVeiculo perfil, bool comPassos, CancellationToken ct)
    {
        if (pontos.Count < 2) return new Trajeto([], Polilinha.Codificar(pontos), "nenhum");
        if (UsarOsrm(perfil))
        {
            try
            {
                return await osrm.ObterTrajetoAsync(pontos, perfil, comPassos, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                log.LogWarning(ex, "OSRM indisponível para o trajeto; usando estimativa em linha reta");
            }
        }
        return await linhaReta.ObterTrajetoAsync(pontos, perfil, comPassos, ct);
    }
}
