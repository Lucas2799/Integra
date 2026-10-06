namespace Intrega.Nucleo.Resultados;

/// <summary>Resultado de um caso de uso: sucesso ou <see cref="Erro"/>, sem usar exceções para fluxo de negócio.</summary>
public class Resultado
{
    protected Resultado(Erro? erro) => Erro = erro;

    public Erro? Erro { get; }
    public bool Sucesso => Erro is null;
    public bool Falha => !Sucesso;

    public static Resultado Ok() => new(null);
    public static Resultado ComFalha(Erro erro) => new(erro);

    public static implicit operator Resultado(Erro erro) => ComFalha(erro);
}

public sealed class Resultado<T> : Resultado
{
    private readonly T? _valor;

    private Resultado(T? valor, Erro? erro) : base(erro) => _valor = valor;

    public T Valor => Sucesso
        ? _valor!
        : throw new InvalidOperationException($"Resultado com falha não possui valor: {Erro!.Codigo}");

    public static Resultado<T> Ok(T valor) => new(valor, null);
    public static new Resultado<T> ComFalha(Erro erro) => new(default, erro);

    public static implicit operator Resultado<T>(T valor) => Ok(valor);
    public static implicit operator Resultado<T>(Erro erro) => ComFalha(erro);
}
