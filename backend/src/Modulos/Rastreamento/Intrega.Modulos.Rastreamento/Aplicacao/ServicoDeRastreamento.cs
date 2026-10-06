using System.ComponentModel.DataAnnotations;
using Intrega.Infraestrutura.TempoReal;
using Intrega.Modulos.Identidade.Contratos;
using Intrega.Modulos.Paradas.Contratos;
using Intrega.Modulos.Rastreamento.Dominio;
using Intrega.Modulos.Rastreamento.Infraestrutura;
using Intrega.Nucleo.Autenticacao;
using Intrega.Nucleo.Geo;
using Intrega.Nucleo.Resultados;
using Intrega.Nucleo.Tempo;
using Microsoft.EntityFrameworkCore;

namespace Intrega.Modulos.Rastreamento.Aplicacao;

public sealed record PosicaoRequisicao(
    [property: Range(-90, 90)] double Latitude,
    [property: Range(-180, 180)] double Longitude,
    DateTimeOffset RegistradaEm,
    double? VelocidadeMetrosPorSegundo = null,
    double? Direcao = null,
    double? PrecisaoMetros = null);

public sealed record LoteDePosicoesRequisicao(
    Guid? RotaId,
    [property: Required, MaxLength(500)] List<PosicaoRequisicao> Posicoes);

public sealed record ConfiguracaoFinanceiraDto(
    [property: Range(0, 1000)] decimal? ValorPorEntrega,
    [property: Range(0, 1000)] decimal? ValorPorPacote,
    [property: Range(0, 50)] decimal? PrecoDoCombustivelPorLitro,
    [property: Range(0, 100)] decimal? KmPorLitro,
    [property: Range(0, 10000)] decimal? CustoFixoPorDia);

public sealed record ResumoPorMarketplaceDto(Marketplace Marketplace, int Entregas, int Pacotes);

public sealed record ResumoDoDiaDto(
    DateOnly Dia, int Entregas, int Pacotes, int NaoEntregues, double DistanciaKm, double HorasAtivas, decimal? GanhoEstimado);

public sealed record ResumoDoPeriodoDto(
    DateOnly De,
    DateOnly Ate,
    int Entregas,
    int Pacotes,
    int NaoEntregues,
    double TaxaDeSucesso,
    double DistanciaKm,
    double HorasAtivas,
    double? MinutosPorEntrega,
    decimal? GanhoEstimado,
    decimal? CustoDeCombustivel,
    decimal? CustoFixo,
    decimal? LucroEstimado,
    IReadOnlyList<ResumoPorMarketplaceDto> PorMarketplace,
    IReadOnlyList<ResumoDoDiaDto> PorDia);

public sealed record PosicaoAoVivoDto(Guid UsuarioId, string Nome, double Latitude, double Longitude, DateTimeOffset RegistradaEm);

/// <summary>Rastreamento e contabilização do tempo: posições, jornada, produtividade e ganhos.</summary>
internal sealed class ServicoDeRastreamento(
    ContextoRastreamento bd,
    IModuloIdentidade identidade,
    IUsuarioAtual usuario,
    INotificadorTempoReal notificador,
    TimeProvider relogio)
{
    public async Task<Resultado> RegistrarPosicoesAsync(LoteDePosicoesRequisicao req, CancellationToken ct)
    {
        var agora = relogio.GetUtcNow();
        var validas = req.Posicoes
            .Where(p => new PontoGeo(p.Latitude, p.Longitude).Valido && p.RegistradaEm <= agora.AddMinutes(5)
                        && p.RegistradaEm > agora.AddDays(-7))
            .ToList();
        if (validas.Count == 0) return Resultado.Ok();

        bd.Posicoes.AddRange(validas.Select(p => AmostraDePosicao.Criar(usuario.Id, req.RotaId, p.Latitude, p.Longitude,
            p.VelocidadeMetrosPorSegundo, p.Direcao, p.PrecisaoMetros, p.RegistradaEm)));
        await bd.SaveChangesAsync(ct);

        // Mapa ao vivo do gestor da frota.
        if (usuario.OrganizacaoId is { } organizacaoId)
        {
            var ultima = validas.MaxBy(p => p.RegistradaEm)!;
            await notificador.NotificarGestoresAsync(organizacaoId, EventosTempoReal.PosicaoEntregador, new
            {
                usuarioId = usuario.Id,
                latitude = ultima.Latitude,
                longitude = ultima.Longitude,
                registradaEm = ultima.RegistradaEm
            }, ct);
        }
        return Resultado.Ok();
    }

    public async Task<Resultado<ResumoDoPeriodoDto>> ResumirAsync(DateOnly? de, DateOnly? ate, CancellationToken ct)
    {
        var hoje = HorarioDeBrasilia.Hoje(relogio);
        var fim = ate ?? hoje;
        var inicio = de ?? fim;
        if (inicio > fim) (inicio, fim) = (fim, inicio);

        var limites = (await identidade.ObterDireitosAsync(usuario.Id, ct)).Limites;
        var diasMaximos = limites.RelatoriosAvancados ? 92 : 7;
        if (fim.DayNumber - inicio.DayNumber + 1 > diasMaximos)
            return limites.RelatoriosAvancados
                ? Erro.Validacao("rastreamento.periodo_longo", $"Escolha um período de até {diasMaximos} dias.")
                : Erro.LimiteDoPlano("plano.relatorios", "No plano gratuito o relatório cobre até 7 dias. Assine o Pro para ver mais.");

        var desde = HorarioDeBrasilia.Em(inicio, TimeOnly.MinValue);
        var antesDe = HorarioDeBrasilia.Em(fim.AddDays(1), TimeOnly.MinValue);

        var registros = await bd.RegistrosDeEntrega.AsNoTracking()
            .Where(r => r.UsuarioId == usuario.Id && r.OcorridoEm >= desde && r.OcorridoEm < antesDe)
            .ToListAsync(ct);
        var posicoes = await bd.Posicoes.AsNoTracking()
            .Where(p => p.UsuarioId == usuario.Id && p.RegistradaEm >= desde && p.RegistradaEm < antesDe)
            .OrderBy(p => p.RegistradaEm)
            .Select(p => new { p.Latitude, p.Longitude, p.RegistradaEm })
            .ToListAsync(ct);
        var financeiro = await bd.ConfiguracoesFinanceiras.AsNoTracking().FirstOrDefaultAsync(c => c.UsuarioId == usuario.Id, ct);

        DateOnly DiaLocal(DateTimeOffset instante) => DateOnly.FromDateTime(HorarioDeBrasilia.ParaLocal(instante).DateTime);

        var dias = new List<ResumoDoDiaDto>();
        for (var dia = inicio; dia <= fim; dia = dia.AddDays(1))
        {
            var doDia = registros.Where(r => DiaLocal(r.OcorridoEm) == dia).ToList();
            var posicoesDoDia = posicoes.Where(p => DiaLocal(p.RegistradaEm) == dia)
                .Select(p => new PontoNoTempo(new PontoGeo(p.Latitude, p.Longitude), p.RegistradaEm)).ToList();
            var entregues = doDia.Where(r => r.Resultado == ResultadoDaTentativa.Entregue).ToList();

            // Sem GPS, usa os horários das entregas para estimar o tempo ativo.
            var instantes = posicoesDoDia.Count > 1 ? posicoesDoDia.Select(p => p.Em).ToList() : doDia.Select(r => r.OcorridoEm).ToList();
            var horas = CalculadoraDeJornada.TempoAtivo(instantes).TotalHours;
            var km = CalculadoraDeJornada.DistanciaPercorridaMetros(posicoesDoDia) / 1000;
            if (doDia.Count == 0 && posicoesDoDia.Count == 0) continue;

            dias.Add(new ResumoDoDiaDto(dia, entregues.Count, entregues.Sum(r => r.QuantidadeDePacotes),
                doDia.Count(r => r.Resultado == ResultadoDaTentativa.NaoEntregue), Math.Round(km, 1), Math.Round(horas, 2),
                Ganho(financeiro, entregues.Count, entregues.Sum(r => r.QuantidadeDePacotes))));
        }

        var entregas = dias.Sum(d => d.Entregas);
        var pacotes = dias.Sum(d => d.Pacotes);
        var naoEntregues = dias.Sum(d => d.NaoEntregues);
        var distanciaKm = dias.Sum(d => d.DistanciaKm);
        var horasAtivas = dias.Sum(d => d.HorasAtivas);
        var ganho = Ganho(financeiro, entregas, pacotes);
        decimal? combustivel = financeiro is { PrecoDoCombustivelPorLitro: { } preco, KmPorLitro: > 0 and var kmPorLitro }
            ? Math.Round((decimal)distanciaKm / kmPorLitro * preco, 2)
            : null;
        decimal? custoFixo = financeiro?.CustoFixoPorDia is { } fixo ? fixo * dias.Count(d => d.Entregas > 0) : null;
        decimal? lucro = ganho is null ? null : ganho - (combustivel ?? 0) - (custoFixo ?? 0);

        var porMarketplace = registros.Where(r => r.Resultado == ResultadoDaTentativa.Entregue)
            .GroupBy(r => r.Marketplace)
            .Select(g => new ResumoPorMarketplaceDto(g.Key, g.Count(), g.Sum(r => r.QuantidadeDePacotes)))
            .OrderByDescending(m => m.Entregas)
            .ToList();

        return new ResumoDoPeriodoDto(inicio, fim, entregas, pacotes, naoEntregues,
            entregas + naoEntregues == 0 ? 0 : Math.Round((double)entregas / (entregas + naoEntregues), 3),
            Math.Round(distanciaKm, 1), Math.Round(horasAtivas, 2),
            entregas == 0 ? null : Math.Round(horasAtivas * 60 / entregas, 1),
            ganho, combustivel, custoFixo, lucro, porMarketplace, dias);
    }

    public async Task<ConfiguracaoFinanceiraDto> ObterConfiguracaoFinanceiraAsync(CancellationToken ct)
    {
        var c = await bd.ConfiguracoesFinanceiras.AsNoTracking().FirstOrDefaultAsync(x => x.UsuarioId == usuario.Id, ct);
        return new ConfiguracaoFinanceiraDto(c?.ValorPorEntrega, c?.ValorPorPacote, c?.PrecoDoCombustivelPorLitro,
            c?.KmPorLitro, c?.CustoFixoPorDia);
    }

    public async Task SalvarConfiguracaoFinanceiraAsync(ConfiguracaoFinanceiraDto dto, CancellationToken ct)
    {
        var c = await bd.ConfiguracoesFinanceiras.FirstOrDefaultAsync(x => x.UsuarioId == usuario.Id, ct);
        if (c is null)
        {
            c = ConfiguracaoFinanceira.Nova(usuario.Id);
            bd.ConfiguracoesFinanceiras.Add(c);
        }
        c.Atualizar(dto.ValorPorEntrega, dto.ValorPorPacote, dto.PrecoDoCombustivelPorLitro, dto.KmPorLitro, dto.CustoFixoPorDia);
        await bd.SaveChangesAsync(ct);
    }

    /// <summary>Última posição de cada entregador da organização nas últimas 2 horas (painel do gestor).</summary>
    public async Task<Resultado<IReadOnlyList<PosicaoAoVivoDto>>> FrotaAoVivoAsync(CancellationToken ct)
    {
        var direitos = await identidade.ObterDireitosAsync(usuario.Id, ct);
        if (direitos.OrganizacaoId is not { } organizacaoId || !direitos.GestorDaOrganizacao)
            return Erro.Proibido("frota.nao_eh_gestor", "Apenas o gestor da organização pode ver a frota.");
        if (!direitos.Limites.GestaoDeFrota)
            return Erro.LimiteDoPlano("plano.frota", "A gestão de frota exige o plano Frota ativo.");

        var membros = await identidade.ListarMembrosAsync(organizacaoId, ct);
        var ids = membros.Select(m => m.UsuarioId).ToList();
        var desde = relogio.GetUtcNow().AddHours(-2);
        var ultimas = await bd.Posicoes.AsNoTracking()
            .Where(p => ids.Contains(p.UsuarioId) && p.RegistradaEm >= desde)
            .GroupBy(p => p.UsuarioId)
            .Select(g => g.OrderByDescending(p => p.RegistradaEm).First())
            .ToListAsync(ct);

        var nomes = membros.ToDictionary(m => m.UsuarioId, m => m.Nome);
        IReadOnlyList<PosicaoAoVivoDto> resultado = ultimas
            .Select(p => new PosicaoAoVivoDto(p.UsuarioId, nomes.GetValueOrDefault(p.UsuarioId, "—"), p.Latitude, p.Longitude, p.RegistradaEm))
            .ToList();
        return Resultado<IReadOnlyList<PosicaoAoVivoDto>>.Ok(resultado);
    }

    private static decimal? Ganho(ConfiguracaoFinanceira? f, int entregas, int pacotes) =>
        f is null || (f.ValorPorEntrega is null && f.ValorPorPacote is null)
            ? null
            : (f.ValorPorEntrega ?? 0) * entregas + (f.ValorPorPacote ?? 0) * pacotes;
}
