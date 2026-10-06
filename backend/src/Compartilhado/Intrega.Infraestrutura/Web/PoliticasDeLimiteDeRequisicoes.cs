namespace Intrega.Infraestrutura.Web;

public static class PoliticasDeLimiteDeRequisicoes
{
    /// <summary>Endpoints sensíveis (login, cadastro): poucas tentativas por IP.</summary>
    public const string Autenticacao = "autenticacao";
}
