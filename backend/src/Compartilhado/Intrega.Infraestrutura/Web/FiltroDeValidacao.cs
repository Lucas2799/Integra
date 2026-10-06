using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Intrega.Infraestrutura.Web;

/// <summary>Valida as DataAnnotations dos argumentos do endpoint (sem depender de bibliotecas externas).</summary>
public sealed class FiltroDeValidacao : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext contexto, EndpointFilterDelegate proximo)
    {
        foreach (var argumento in contexto.Arguments)
        {
            if (argumento is null || argumento.GetType().IsPrimitive || argumento is string or CancellationToken) continue;
            if (argumento.GetType().Namespace?.StartsWith("Intrega", StringComparison.Ordinal) != true) continue;

            var resultados = new List<ValidationResult>();
            if (!Validator.TryValidateObject(argumento, new ValidationContext(argumento), resultados, true))
            {
                var erros = resultados
                    .SelectMany(r => r.MemberNames.DefaultIfEmpty(string.Empty), (r, campo) => (Campo: campo, r.ErrorMessage))
                    .GroupBy(x => x.Campo)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage ?? "inválido").ToArray());
                return Results.ValidationProblem(erros);
            }
        }
        return await proximo(contexto);
    }
}

public static class ExtensoesDeValidacao
{
    public static TBuilder ComValidacao<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(new FiltroDeValidacao());
}
