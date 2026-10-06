using Intrega.Nucleo.Resultados;
using Microsoft.AspNetCore.Http;

namespace Intrega.Infraestrutura.Web;

public static class ExtensoesDeResultadoHttp
{
    /// <summary>Tipo de problema que o app usa para abrir a tela de assinatura (paywall).</summary>
    public const string TipoProblemaLimiteDoPlano = "https://intrega.app/problemas/limite-do-plano";

    public static IResult ParaHttp<T>(this Resultado<T> resultado, Func<T, IResult>? aoSucesso = null) =>
        resultado.Sucesso
            ? aoSucesso?.Invoke(resultado.Valor) ?? Results.Ok(resultado.Valor)
            : resultado.Erro!.ParaProblema();

    public static IResult ParaHttp(this Resultado resultado) =>
        resultado.Sucesso ? Results.NoContent() : resultado.Erro!.ParaProblema();

    public static IResult ParaProblema(this Erro erro)
    {
        var (status, tipo) = erro.Tipo switch
        {
            TipoErro.Validacao => (StatusCodes.Status400BadRequest, "https://intrega.app/problemas/validacao"),
            TipoErro.NaoEncontrado => (StatusCodes.Status404NotFound, "https://intrega.app/problemas/nao-encontrado"),
            TipoErro.Conflito => (StatusCodes.Status409Conflict, "https://intrega.app/problemas/conflito"),
            TipoErro.Proibido => (StatusCodes.Status403Forbidden, "https://intrega.app/problemas/proibido"),
            TipoErro.LimiteDoPlano => (StatusCodes.Status402PaymentRequired, TipoProblemaLimiteDoPlano),
            _ => (StatusCodes.Status500InternalServerError, "https://intrega.app/problemas/inesperado")
        };

        return Results.Problem(
            statusCode: status,
            type: tipo,
            title: erro.Codigo,
            detail: erro.Mensagem,
            extensions: new Dictionary<string, object?> { ["codigo"] = erro.Codigo });
    }
}
