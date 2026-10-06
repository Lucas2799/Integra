using Intrega.Infraestrutura.Web;
using Intrega.Modulos.Identidade.Aplicacao;
using Intrega.Nucleo.Autenticacao;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Intrega.Modulos.Identidade.Endpoints;

internal static class EndpointsIdentidade
{
    public static void Mapear(IEndpointRouteBuilder app)
    {
        var autenticacao = app.MapGroup("/api/autenticacao").WithTags("Autenticação")
            .RequireRateLimiting(PoliticasDeLimiteDeRequisicoes.Autenticacao).ComValidacao();
        autenticacao.MapPost("/cadastrar", async (CadastroRequisicao req, ServicoDeAutenticacao s, CancellationToken ct) =>
            (await s.CadastrarAsync(req, ct)).ParaHttp());
        autenticacao.MapPost("/entrar", async (EntrarRequisicao req, ServicoDeAutenticacao s, CancellationToken ct) =>
            (await s.EntrarAsync(req, ct)).ParaHttp());
        autenticacao.MapPost("/renovar", async (TokenDeAtualizacaoRequisicao req, ServicoDeAutenticacao s, CancellationToken ct) =>
            (await s.RenovarAsync(req, ct)).ParaHttp());
        autenticacao.MapPost("/sair", async (TokenDeAtualizacaoRequisicao req, ServicoDeAutenticacao s, CancellationToken ct) =>
        {
            await s.SairAsync(req, ct);
            return Results.NoContent();
        });

        var conta = app.MapGroup("/api/conta").WithTags("Conta").RequireAuthorization().ComValidacao();
        conta.MapGet("/", async (IUsuarioAtual usuario, ServicoDeConta s, CancellationToken ct) =>
            (await s.ObterAsync(usuario.Id, ct)).ParaHttp());
        conta.MapPut("/", async (AtualizarPerfilRequisicao req, IUsuarioAtual usuario, ServicoDeConta s, CancellationToken ct) =>
            (await s.AtualizarPerfilAsync(usuario.Id, req, ct)).ParaHttp());
        conta.MapDelete("/", async (IUsuarioAtual usuario, ServicoDeConta s, CancellationToken ct) =>
                (await s.ExcluirAsync(usuario.Id, ct)).ParaHttp())
            .WithSummary("Exclui a conta e todos os dados do usuário (LGPD).");

        var assinatura = app.MapGroup("/api/assinatura").WithTags("Assinatura").ComValidacao();
        assinatura.MapGet("/planos", () => Results.Ok(ServicoDeAssinatura.ListarPlanos()));
        assinatura.MapPost("/validar-compra", async (ValidarCompraRequisicao req, IUsuarioAtual usuario, ServicoDeAssinatura s, CancellationToken ct) =>
                (await s.ValidarCompraAsync(usuario.Id, req, ct)).ParaHttp())
            .RequireAuthorization();

        var organizacoes = app.MapGroup("/api/organizacoes").WithTags("Organizações (B2B)").RequireAuthorization().ComValidacao();
        organizacoes.MapPost("/", async (CriarOrganizacaoRequisicao req, IUsuarioAtual usuario, ServicoDeOrganizacoes s, CancellationToken ct) =>
            (await s.CriarAsync(usuario.Id, req, ct)).ParaHttp());
        organizacoes.MapPost("/entrar", async (EntrarNaOrganizacaoRequisicao req, IUsuarioAtual usuario, ServicoDeOrganizacoes s, CancellationToken ct) =>
            (await s.EntrarAsync(usuario.Id, req, ct)).ParaHttp());
        organizacoes.MapGet("/minha", async (IUsuarioAtual usuario, ServicoDeOrganizacoes s, CancellationToken ct) =>
            (await s.ObterMinhaAsync(usuario.Id, ct)).ParaHttp());
        organizacoes.MapPost("/minha/codigo-de-convite", async (IUsuarioAtual usuario, ServicoDeOrganizacoes s, CancellationToken ct) =>
            (await s.GerarNovoConviteAsync(usuario.Id, ct)).ParaHttp(codigo => Results.Ok(new { codigoDeConvite = codigo })));
        organizacoes.MapDelete("/minha/membros/{membroId:guid}", async (Guid membroId, IUsuarioAtual usuario, ServicoDeOrganizacoes s, CancellationToken ct) =>
            (await s.RemoverMembroAsync(usuario.Id, membroId, ct)).ParaHttp());
    }
}
