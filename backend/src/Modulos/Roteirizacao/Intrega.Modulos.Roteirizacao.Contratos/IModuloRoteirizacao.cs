using Intrega.Nucleo.Eventos;
using Intrega.Nucleo.Geo;

namespace Intrega.Modulos.Roteirizacao.Contratos;

public enum StatusDaRota
{
    Rascunho,
    Planejada,
    EmAndamento,
    Concluida
}

public enum PerfilDeVeiculo
{
    Carro,
    Moto,
    Bicicleta,
    APe
}

/// <summary>Quem pode ver/alterar a rota: o dono, o entregador designado ou o gestor da organização.</summary>
public sealed record AcessoARota(
    Guid RotaId,
    Guid DonoId,
    Guid EntregadorId,
    Guid? OrganizacaoId,
    StatusDaRota Status,
    DateOnly Data,
    /// <summary>Ponto de saída: endereços ambíguos ("Rua Augusta") são procurados perto dele.</summary>
    PontoGeo Saida)
{
    public bool PodeSerAcessadaPor(Guid usuarioId, Guid? organizacaoId, bool gestor) =>
        usuarioId == DonoId || usuarioId == EntregadorId ||
        (gestor && OrganizacaoId is not null && OrganizacaoId == organizacaoId);
}

public interface IModuloRoteirizacao
{
    Task<AcessoARota?> ObterAcessoAsync(Guid rotaId, CancellationToken ct = default);
}

/// <summary>A sequência ou o ETA da rota mudaram (otimização, recálculo, entrega). O app é avisado em tempo real.</summary>
public sealed record RotaAtualizada(Guid RotaId, Guid EntregadorId, Guid? OrganizacaoId, string Motivo) : EventoDeIntegracao;
