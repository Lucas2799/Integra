using Intrega.Modulos.Paradas.Contratos;
using Intrega.Modulos.Paradas.Dominio;

namespace Intrega.Modulos.Paradas.Aplicacao;

internal static class Mapeamento
{
    public static ParadaDto ParaDto(this Parada p) => new(
        p.Id, p.ResponsavelId, p.OrganizacaoId, p.RotaId, p.Status, p.Endereco.ParaContrato(), p.Local,
        p.ConfiancaDaLocalizacao, p.LocalConfirmado, p.PrecisaConfirmarLocal, p.NomeDoDestinatario,
        p.TelefoneDoDestinatario, p.Observacoes, p.CodigosDePacote, p.QuantidadeDePacotes, p.Marketplace, p.Origem,
        p.JanelaInicio, p.JanelaFim, p.Prioridade, p.TempoDeAtendimentoSegundos, p.Tentativas, p.UltimoMotivoDeFalha,
        p.EstrategiaDeNovaTentativa, p.NovaTentativaApos, p.EntregueEm, p.RecebidoPor, p.CaminhoDoComprovante is not null,
        p.CriadoEm);

    public static ParadaParaPlanejamento ParaPlanejamento(this Parada p) => new(
        p.Id, p.Local, p.TempoDeAtendimentoSegundos, p.JanelaInicio, p.JanelaFim, p.Prioridade, p.Status, p.Tentativas,
        p.EstrategiaDeNovaTentativa, p.NovaTentativaApos);
}
