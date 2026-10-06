using Intrega.Modulos.Identidade.Contratos;
using Intrega.Modulos.Identidade.Dominio;
using Intrega.Modulos.Identidade.Infraestrutura;
using Intrega.Nucleo.Autenticacao;
using Intrega.Nucleo.Resultados;
using Microsoft.EntityFrameworkCore;

namespace Intrega.Modulos.Identidade.Aplicacao;

internal sealed class ServicoDeAssinatura(
    ContextoIdentidade bd,
    ApiDoModuloIdentidade api,
    IVerificadorDeCompraNaLoja verificador)
{
    public static PlanosResposta ListarPlanos() => new(
        [
            new PlanoDto(Plano.Gratuito, CatalogoDePlanos.Gratuito),
            new PlanoDto(Plano.Pro, CatalogoDePlanos.Pro),
            new PlanoDto(Plano.Frota, CatalogoDePlanos.Frota)
        ],
        CatalogoDeProdutos.Todos
            .Select(p => new ProdutoDto(p.Id, p.Plano, p.DiasDeVigencia, p.PrecoEmReais, p.Descricao))
            .ToList());

    public async Task<Resultado> ValidarCompraAsync(Guid usuarioId, ValidarCompraRequisicao req, CancellationToken ct)
    {
        var hashDoComprovante = ServicoDeTokens.Hash(req.Comprovante);
        if (await bd.Compras.AnyAsync(c => c.HashDoComprovante == hashDoComprovante, ct))
            return Erro.Conflito("assinatura.compra_ja_usada", "Esta compra já foi registrada.");

        var verificada = await verificador.VerificarAsync(req.Plataforma, req.ProdutoId, req.Comprovante, ct);
        if (verificada is null)
            return Erro.Validacao("assinatura.compra_invalida", "Não foi possível validar a compra junto à loja.");

        var usuario = await bd.Usuarios.FirstAsync(u => u.Id == usuarioId, ct);
        if (verificada.Plano == Plano.Frota)
        {
            if (usuario.OrganizacaoId is null || usuario.PapelNaOrganizacao != ClaimsIntrega.PapelGestor)
                return Erro.Proibido("assinatura.frota_exige_gestor", "O plano Frota é contratado pelo gestor da organização.");

            var organizacao = await bd.Organizacoes.FirstAsync(o => o.Id == usuario.OrganizacaoId, ct);
            organizacao.EstenderPlano(verificada.ExpiraEm);
            var membros = await bd.Usuarios.Where(u => u.OrganizacaoId == organizacao.Id).Select(u => u.Id).ToListAsync(ct);
            membros.ForEach(api.Invalidar);
        }
        else
        {
            usuario.AtivarPlano(verificada.Plano, verificada.ExpiraEm);
        }

        bd.Compras.Add(Compra.Registrar(usuarioId, req.Plataforma, req.ProdutoId, hashDoComprovante,
            verificada.Plano, verificada.ExpiraEm));
        await bd.SaveChangesAsync(ct);
        api.Invalidar(usuarioId);
        return Resultado.Ok();
    }
}
