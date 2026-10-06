namespace Intrega.Nucleo.Resultados;

public enum TipoErro
{
    Validacao,
    NaoEncontrado,
    Conflito,
    Proibido,
    /// <summary>Recurso bloqueado pelo plano atual (o app abre a tela de assinatura).</summary>
    LimiteDoPlano,
    Inesperado
}

/// <summary>Erro de negócio com código estável (usado pelo app) e mensagem para o usuário.</summary>
public sealed record Erro(string Codigo, string Mensagem, TipoErro Tipo = TipoErro.Validacao)
{
    public static Erro Validacao(string codigo, string mensagem) => new(codigo, mensagem, TipoErro.Validacao);
    public static Erro NaoEncontrado(string codigo, string mensagem) => new(codigo, mensagem, TipoErro.NaoEncontrado);
    public static Erro Conflito(string codigo, string mensagem) => new(codigo, mensagem, TipoErro.Conflito);
    public static Erro Proibido(string codigo, string mensagem) => new(codigo, mensagem, TipoErro.Proibido);
    public static Erro LimiteDoPlano(string codigo, string mensagem) => new(codigo, mensagem, TipoErro.LimiteDoPlano);
}
