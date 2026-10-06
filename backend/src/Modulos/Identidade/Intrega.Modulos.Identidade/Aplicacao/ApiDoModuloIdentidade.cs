using Intrega.Modulos.Identidade.Contratos;
using Intrega.Modulos.Identidade.Dominio;
using Intrega.Modulos.Identidade.Infraestrutura;
using Intrega.Nucleo.Autenticacao;
using Intrega.Nucleo.Resultados;
using Intrega.Nucleo.Tempo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Intrega.Modulos.Identidade.Aplicacao;

/// <summary>Implementação da API pública do módulo: direitos do plano e medição de uso do freemium.</summary>
internal sealed class ApiDoModuloIdentidade(ContextoIdentidade bd, IMemoryCache cache, TimeProvider relogio) : IModuloIdentidade
{
    private static readonly TimeSpan TempoDeCache = TimeSpan.FromSeconds(60);

    private static string ChaveDeCache(Guid usuarioId) => $"identidade:direitos:{usuarioId}";

    public async Task<DireitosDoUsuario> ObterDireitosAsync(Guid usuarioId, CancellationToken ct = default)
    {
        if (cache.TryGetValue(ChaveDeCache(usuarioId), out DireitosDoUsuario? emCache) && emCache is not null)
            return emCache;

        var usuario = await bd.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
            ?? throw new UnauthorizedAccessException("Usuário não encontrado.");

        var agora = relogio.GetUtcNow();
        var plano = usuario.PlanoEfetivo(agora);
        var expiraEm = usuario.PlanoExpiraEm;

        // Membro de organização com plano ativo usa o plano Frota da organização.
        if (usuario.OrganizacaoId is { } organizacaoId)
        {
            var organizacao = await bd.Organizacoes.AsNoTracking().FirstOrDefaultAsync(o => o.Id == organizacaoId, ct);
            if (organizacao is not null && organizacao.PlanoAtivo(agora))
            {
                plano = Plano.Frota;
                expiraEm = organizacao.PlanoExpiraEm;
            }
        }

        var direitos = new DireitosDoUsuario(usuario.Id, plano, CatalogoDePlanos.De(plano), expiraEm,
            usuario.OrganizacaoId, usuario.PapelNaOrganizacao == ClaimsIntrega.PapelGestor);
        cache.Set(ChaveDeCache(usuarioId), direitos, TempoDeCache);
        return direitos;
    }

    public async Task<Resultado> ConsumirUsoAsync(Guid usuarioId, RecursoMedido recurso, CancellationToken ct = default)
    {
        var direitos = await ObterDireitosAsync(usuarioId, ct);
        var limite = recurso switch
        {
            RecursoMedido.Otimizacao => direitos.Limites.MaximoOtimizacoesPorDia,
            RecursoMedido.LeituraDeEtiqueta => direitos.Limites.MaximoLeiturasPorDia,
            _ => null
        };

        var hoje = HorarioDeBrasilia.Hoje(relogio);
        var registro = await bd.RegistrosDeUso.FirstOrDefaultAsync(
            r => r.UsuarioId == usuarioId && r.Recurso == recurso && r.Dia == hoje, ct);

        if (limite is { } maximo && registro is not null && registro.Quantidade >= maximo)
        {
            var nome = recurso == RecursoMedido.Otimizacao ? "otimizacoes" : "leituras";
            return Erro.LimiteDoPlano($"plano.limite_{nome}",
                $"Limite diário do plano gratuito atingido ({maximo}). Assine o Pro para uso ilimitado.");
        }

        if (registro is null)
        {
            registro = RegistroDeUso.Iniciar(usuarioId, recurso, hoje);
            bd.RegistrosDeUso.Add(registro);
        }
        registro.Incrementar();

        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Duas requisições ao mesmo tempo: recomeça com os dados atualizados.
            bd.ChangeTracker.Clear();
            return await ConsumirUsoAsync(usuarioId, recurso, ct);
        }
        return Resultado.Ok();
    }

    public async Task<IReadOnlyList<MembroDaOrganizacaoDto>> ListarMembrosAsync(Guid organizacaoId, CancellationToken ct = default) =>
        await bd.Usuarios.AsNoTracking()
            .Where(u => u.OrganizacaoId == organizacaoId)
            .OrderBy(u => u.Nome)
            .Select(u => new MembroDaOrganizacaoDto(u.Id, u.Nome, u.Email, u.PapelNaOrganizacao ?? ClaimsIntrega.PapelEntregador))
            .ToListAsync(ct);

    public async Task<UsoDeHojeDto> ObterUsoDeHojeAsync(Guid usuarioId, CancellationToken ct)
    {
        var hoje = HorarioDeBrasilia.Hoje(relogio);
        var registros = await bd.RegistrosDeUso.AsNoTracking()
            .Where(r => r.UsuarioId == usuarioId && r.Dia == hoje)
            .ToListAsync(ct);
        return new UsoDeHojeDto(
            registros.FirstOrDefault(r => r.Recurso == RecursoMedido.Otimizacao)?.Quantidade ?? 0,
            registros.FirstOrDefault(r => r.Recurso == RecursoMedido.LeituraDeEtiqueta)?.Quantidade ?? 0);
    }

    /// <summary>Descarta o cache após mudança de plano/organização.</summary>
    public void Invalidar(Guid usuarioId) => cache.Remove(ChaveDeCache(usuarioId));
}
