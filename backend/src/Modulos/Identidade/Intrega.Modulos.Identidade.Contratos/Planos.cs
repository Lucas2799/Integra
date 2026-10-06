namespace Intrega.Modulos.Identidade.Contratos;

public enum Plano
{
    Gratuito = 0,
    Pro = 1,
    /// <summary>B2B: gestor + entregadores de uma organização.</summary>
    Frota = 2
}

/// <summary>Limites de cada plano. <c>null</c> significa ilimitado.</summary>
public sealed record LimitesDoPlano(
    int? MaximoParadasPorRota,
    int? MaximoOtimizacoesPorDia,
    int? MaximoLeiturasPorDia,
    int? MaximoRecalculosPorRota,
    bool ImportarPlanilha,
    bool JanelasDeHorario,
    bool NavegacaoNoApp,
    bool ComprovanteDeEntrega,
    bool RelatoriosAvancados,
    bool GestaoDeFrota);

/// <summary>Fonte única dos limites do freemium. Mudar um limite = mudar aqui.</summary>
public static class CatalogoDePlanos
{
    public static readonly LimitesDoPlano Gratuito = new(
        MaximoParadasPorRota: 25,
        MaximoOtimizacoesPorDia: 2,
        MaximoLeiturasPorDia: 10,
        MaximoRecalculosPorRota: 1,
        ImportarPlanilha: false,
        JanelasDeHorario: false,
        NavegacaoNoApp: false,
        ComprovanteDeEntrega: false,
        RelatoriosAvancados: false,
        GestaoDeFrota: false);

    public static readonly LimitesDoPlano Pro = new(
        MaximoParadasPorRota: null,
        MaximoOtimizacoesPorDia: null,
        MaximoLeiturasPorDia: null,
        MaximoRecalculosPorRota: null,
        ImportarPlanilha: true,
        JanelasDeHorario: true,
        NavegacaoNoApp: true,
        ComprovanteDeEntrega: true,
        RelatoriosAvancados: true,
        GestaoDeFrota: false);

    public static readonly LimitesDoPlano Frota = Pro with { GestaoDeFrota = true };

    public static LimitesDoPlano De(Plano plano) => plano switch
    {
        Plano.Pro => Pro,
        Plano.Frota => Frota,
        _ => Gratuito
    };
}

/// <summary>Recursos com limite diário de uso no plano gratuito.</summary>
public enum RecursoMedido
{
    Otimizacao,
    LeituraDeEtiqueta
}

/// <summary>O que o usuário pode fazer agora: plano efetivo (individual ou da organização) e seus limites.</summary>
public sealed record DireitosDoUsuario(
    Guid UsuarioId,
    Plano Plano,
    LimitesDoPlano Limites,
    DateTimeOffset? PlanoExpiraEm,
    Guid? OrganizacaoId,
    bool GestorDaOrganizacao);
