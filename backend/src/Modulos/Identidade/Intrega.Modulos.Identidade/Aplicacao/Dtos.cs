using System.ComponentModel.DataAnnotations;
using Intrega.Modulos.Identidade.Contratos;

namespace Intrega.Modulos.Identidade.Aplicacao;

public sealed record CadastroRequisicao(
    [property: Required, StringLength(120, MinimumLength = 2)] string Nome,
    [property: Required, EmailAddress] string Email,
    [property: Required, StringLength(128, MinimumLength = 8)] string Senha);

public sealed record EntrarRequisicao(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Senha);

public sealed record TokenDeAtualizacaoRequisicao([property: Required] string TokenDeAtualizacao);

public sealed record SessaoResposta(
    string TokenDeAcesso,
    DateTimeOffset AcessoExpiraEm,
    string TokenDeAtualizacao,
    DateTimeOffset AtualizacaoExpiraEm);

public sealed record AtualizarPerfilRequisicao(
    [property: Required, StringLength(120, MinimumLength = 2)] string Nome,
    [property: Phone] string? Telefone);

public sealed record UsoDeHojeDto(int Otimizacoes, int LeiturasDeEtiqueta);

public sealed record ResumoDaOrganizacaoDto(Guid Id, string Nome, string Papel);

public sealed record ContaResposta(
    Guid Id,
    string Nome,
    string Email,
    string? Telefone,
    Plano Plano,
    DateTimeOffset? PlanoExpiraEm,
    bool EmPeriodoDeTeste,
    LimitesDoPlano Limites,
    UsoDeHojeDto UsoDeHoje,
    ResumoDaOrganizacaoDto? Organizacao);

public sealed record CriarOrganizacaoRequisicao([property: Required, StringLength(120, MinimumLength = 2)] string Nome);

public sealed record EntrarNaOrganizacaoRequisicao([property: Required, StringLength(16, MinimumLength = 6)] string CodigoDeConvite);

public sealed record OrganizacaoDto(
    Guid Id,
    string Nome,
    string? CodigoDeConvite,
    DateTimeOffset? PlanoExpiraEm,
    IReadOnlyList<MembroDaOrganizacaoDto> Membros);

public sealed record PlanoDto(Plano Plano, LimitesDoPlano Limites);

public sealed record ProdutoDto(string Id, Plano Plano, int DiasDeVigencia, decimal PrecoEmReais, string Descricao);

public sealed record PlanosResposta(IReadOnlyList<PlanoDto> Planos, IReadOnlyList<ProdutoDto> Produtos);

public sealed record ValidarCompraRequisicao(
    [property: Required, RegularExpression("^(android|ios)$")] string Plataforma,
    [property: Required] string ProdutoId,
    [property: Required] string Comprovante);
