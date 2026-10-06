using System.ComponentModel.DataAnnotations;

namespace Intrega.Modulos.Identidade.Infraestrutura;

/// <summary>Seção "Autenticacao" do appsettings.</summary>
public sealed class OpcoesDeAutenticacao
{
    public const string Secao = "Autenticacao";

    [Required] public string Emissor { get; set; } = "intrega";
    [Required] public string Publico { get; set; } = "intrega-app";

    /// <summary>Chave HMAC com no mínimo 32 caracteres. Em produção vem de variável de ambiente.</summary>
    [Required, MinLength(32)] public string ChaveDeAssinatura { get; set; } = default!;

    public int MinutosDoTokenDeAcesso { get; set; } = 60;
    public int DiasDoTokenDeAtualizacao { get; set; } = 30;
}

/// <summary>Seção "Assinatura" do appsettings.</summary>
public sealed class OpcoesDeAssinatura
{
    public const string Secao = "Assinatura";

    /// <summary>Dias de Pro grátis para novos usuários.</summary>
    public int DiasDeTeste { get; set; } = 7;

    /// <summary>Dias de Frota grátis para novas organizações.</summary>
    public int DiasDeTesteFrota { get; set; } = 14;

    /// <summary>Aceita comprovantes "dev-*" sem validar na loja. NUNCA habilitar em produção.</summary>
    public bool PermitirComprasDeTeste { get; set; }
}
