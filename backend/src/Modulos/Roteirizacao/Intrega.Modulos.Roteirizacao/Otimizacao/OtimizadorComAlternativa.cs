using Intrega.Modulos.Roteirizacao.Motor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Intrega.Modulos.Roteirizacao.Otimizacao;

/// <summary>Usa o OR-Tools; se a biblioteca nativa faltar ou o solver falhar, usa a heurística.</summary>
internal sealed class OtimizadorComAlternativa(
    OtimizadorOrTools orTools,
    OtimizadorHeuristico heuristico,
    IOptions<OpcoesDeRoteirizacao> opcoes,
    ILogger<OtimizadorComAlternativa> log) : IOtimizadorDeRotas
{
    public SolucaoDeRoteirizacao Otimizar(ProblemaDeRoteirizacao problema, CancellationToken ct)
    {
        if (!opcoes.Value.Otimizador.Equals("Heuristica", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return orTools.Otimizar(problema with { TempoLimiteSegundos = opcoes.Value.TempoLimiteDoOtimizadorSegundos }, ct);
            }
            catch (Exception ex) when (ex is DllNotFoundException or TypeInitializationException or InvalidOperationException
                                           or BadImageFormatException or EntryPointNotFoundException)
            {
                log.LogWarning(ex, "OR-Tools indisponível; usando a heurística");
            }
        }
        return heuristico.Otimizar(problema, ct);
    }
}
