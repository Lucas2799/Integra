using Intrega.Modulos.Geocodificacao.Contratos;
using Intrega.Modulos.Identidade.Contratos;
using Intrega.Modulos.Paradas.Contratos;
using Intrega.Modulos.Paradas.Dominio;
using Intrega.Modulos.Paradas.Infraestrutura;
using Intrega.Nucleo.Resultados;
using Microsoft.EntityFrameworkCore;

namespace Intrega.Modulos.Paradas.Aplicacao;

/// <summary>Implementação da API pública do módulo, usada por Roteirização, Importação e Etiquetas.</summary>
internal sealed class ApiDoModuloParadas(
    ContextoParadas bd,
    IModuloGeocodificacao geocodificacao,
    IModuloIdentidade identidade,
    Intrega.Modulos.Roteirizacao.Contratos.IModuloRoteirizacao roteirizacao)
    : IModuloParadas
{
    public async Task<IReadOnlyList<ParadaParaPlanejamento>> ListarParaPlanejamentoAsync(Guid rotaId, CancellationToken ct = default) =>
        (await bd.Paradas.AsNoTracking().Where(p => p.RotaId == rotaId).ToListAsync(ct))
        .Select(p => p.ParaPlanejamento()).ToList();

    public async Task<IReadOnlyList<ParadaDto>> ListarDaRotaAsync(Guid rotaId, CancellationToken ct = default) =>
        (await bd.Paradas.AsNoTracking().Where(p => p.RotaId == rotaId).OrderBy(p => p.CriadoEm).ToListAsync(ct))
        .Select(p => p.ParaDto()).ToList();

    public async Task<IReadOnlyList<ParadaDto>> ListarPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        (await bd.Paradas.AsNoTracking().Where(p => ids.Contains(p.Id)).ToListAsync(ct))
        .Select(p => p.ParaDto()).ToList();

    public async Task<Resultado<ResultadoDaInclusao>> IncluirAsync(ResponsavelPelaParada responsavel, Guid? rotaId,
        IReadOnlyList<NovaParada> paradas, OrigemDaParada origem, Action<int>? aoProgredir = null, CancellationToken ct = default)
    {
        var limites = (await identidade.ObterDireitosAsync(responsavel.UsuarioId, ct)).Limites;
        var jaNaRota = rotaId is { } r ? await bd.Paradas.CountAsync(p => p.RotaId == r, ct) : 0;
        var pendentesDaRota = rotaId is { } id
            ? await bd.Paradas.Where(p => p.RotaId == id && p.Status == StatusDaParada.Pendente).ToListAsync(ct)
            : [];

        var regiaoDaRota = rotaId is { } rid ? (await roteirizacao.ObterAcessoAsync(rid, ct))?.Saida : null;
        var criadas = new List<Parada>();
        var erros = new List<ErroNaInclusao>();
        var semLocalizacao = 0;

        for (var i = 0; i < paradas.Count; i++)
        {
            aoProgredir?.Invoke(i);
            var dados = paradas[i];
            // Janelas de horário são do Pro: no gratuito são ignoradas na inclusão em lote.
            if (!limites.JanelasDeHorario) dados = dados with { JanelaInicio = null, JanelaFim = null };

            if (limites.MaximoParadasPorRota is { } maximo && rotaId is not null && jaNaRota + criadas.Count >= maximo)
            {
                erros.Add(new ErroNaInclusao(i, $"Limite de {maximo} paradas por rota do plano gratuito."));
                continue;
            }

            var parada = Parada.Criar(responsavel, rotaId, dados, origem);
            await LocalizarAsync(parada, dados, regiaoDaRota, ct);
            if (parada.Local is null) semLocalizacao++;

            // Mesmo endereço já na rota: junta os pacotes numa parada só.
            var mesmoEndereco = pendentesDaRota.Concat(criadas).FirstOrDefault(p =>
                string.Equals(p.Endereco.Descricao, parada.Endereco.Descricao, StringComparison.OrdinalIgnoreCase));
            if (mesmoEndereco is not null)
            {
                mesmoEndereco.Agrupar(parada);
                continue;
            }

            bd.Paradas.Add(parada);
            criadas.Add(parada);
        }
        aoProgredir?.Invoke(paradas.Count);

        await bd.SaveChangesAsync(ct);
        return new ResultadoDaInclusao(criadas.Count, criadas.Count(p => p.PrecisaConfirmarLocal), semLocalizacao,
            criadas.Select(p => p.Id).ToList(), erros);
    }

    /// <summary>Define endereço e coordenada: usa o ponto informado ou geocodifica o endereço.</summary>
    internal async Task LocalizarAsync(Parada parada, NovaParada dados, Intrega.Nucleo.Geo.PontoGeo? regiao, CancellationToken ct)
    {
        if (dados.Local is { Valido: true } conhecido)
        {
            // Só o pino (sem endereço digitado): descobre o endereço pela coordenada.
            var semEndereco = string.IsNullOrWhiteSpace(dados.Endereco.Logradouro) && string.IsNullOrWhiteSpace(dados.Endereco.TextoLivre);
            var reverso = semEndereco ? await geocodificacao.GeocodificacaoReversaAsync(conhecido, ct) : null;
            parada.DefinirEndereco(reverso is not null
                ? reverso with { Complemento = dados.Endereco.Complemento }
                : EnderecoInformadoParaNormalizado(dados.Endereco));
            parada.ConfirmarLocal(conhecido);
            return;
        }

        var resultado = await geocodificacao.GeocodificarAsync(dados.Endereco, regiao, ct);
        if (resultado is null)
        {
            parada.DefinirEndereco(EnderecoInformadoParaNormalizado(dados.Endereco));
            return;
        }
        parada.DefinirEndereco(resultado.Endereco);
        parada.DefinirLocalizacao(resultado.Local, resultado.Confianca, resultado.Provedor);
    }

    private static EnderecoNormalizado EnderecoInformadoParaNormalizado(EnderecoInformado e)
    {
        var partes = new[] { e.Logradouro, e.Numero, e.Complemento, e.Bairro, e.Cidade, e.Uf }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        var descricao = string.Join(", ", partes);
        if (descricao.Length == 0) descricao = e.TextoLivre ?? e.Cep ?? "Endereço sem descrição";
        return new EnderecoNormalizado(e.Logradouro, e.Numero, e.Complemento, e.Bairro, e.Cidade, e.Uf, e.Cep, descricao);
    }

    public async Task<ParadaDto?> BuscarPorCodigoDePacoteAsync(Guid responsavelId, string codigo, CancellationToken ct = default)
    {
        var parada = await bd.Paradas.AsNoTracking()
            .Where(p => p.ResponsavelId == responsavelId && p.CodigosDePacote.Contains(codigo))
            .OrderByDescending(p => p.CriadoEm)
            .FirstOrDefaultAsync(ct);
        return parada?.ParaDto();
    }

    public async Task<int> AtribuirARotaAsync(IReadOnlyCollection<Guid> paradaIds, Guid rotaId, CancellationToken ct = default)
    {
        var paradas = await bd.Paradas.Where(p => paradaIds.Contains(p.Id)).ToListAsync(ct);
        foreach (var parada in paradas)
        {
            parada.AtribuirARota(rotaId);
            if (parada.Status == StatusDaParada.Adiada) parada.Reabrir();
        }
        await bd.SaveChangesAsync(ct);
        return paradas.Count;
    }

    public async Task<IReadOnlyDictionary<Guid, ContagemDaRota>> ContarPorRotasAsync(
        IReadOnlyCollection<Guid> rotaIds, CancellationToken ct = default)
    {
        var linhas = await bd.Paradas.AsNoTracking()
            .Where(p => p.RotaId != null && rotaIds.Contains(p.RotaId.Value))
            .GroupBy(p => p.RotaId!.Value)
            .Select(g => new
            {
                RotaId = g.Key,
                Total = g.Count(),
                Entregues = g.Count(p => p.Status == StatusDaParada.Entregue),
                Pendentes = g.Count(p => p.Status == StatusDaParada.Pendente),
                ComFalha = g.Count(p => p.Status == StatusDaParada.Devolvida || p.Tentativas > 0)
            })
            .ToListAsync(ct);
        return linhas.ToDictionary(l => l.RotaId, l => new ContagemDaRota(l.Total, l.Entregues, l.Pendentes, l.ComFalha));
    }

    public async Task DesvincularDaRotaAsync(Guid rotaId, CancellationToken ct = default)
    {
        var paradas = await bd.Paradas.Where(p => p.RotaId == rotaId && p.Status == StatusDaParada.Pendente).ToListAsync(ct);
        paradas.ForEach(p => p.AtribuirARota(null));
        await bd.SaveChangesAsync(ct);
    }
}
