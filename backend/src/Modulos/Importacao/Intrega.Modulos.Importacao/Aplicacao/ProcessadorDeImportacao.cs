using Intrega.Infraestrutura.TempoReal;
using Intrega.Modulos.Importacao.Dominio;
using Intrega.Modulos.Importacao.Infraestrutura;
using Intrega.Modulos.Importacao.Leitura;
using Intrega.Modulos.Paradas.Contratos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Intrega.Modulos.Importacao.Aplicacao;

/// <summary>
/// Processa as importações em segundo plano, em lotes, avisando o progresso ao app.
/// A geocodificação gratuita (Nominatim) é limitada a 1 consulta/s, por isso não dá para fazer na requisição.
/// </summary>
internal sealed class ProcessadorDeImportacao(
    FilaDeImportacao fila,
    IServiceScopeFactory fabricaDeEscopos,
    TimeProvider relogio,
    ILogger<ProcessadorDeImportacao> log) : BackgroundService
{
    private const int TamanhoDoLote = 10;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await RetomarInterrompidasAsync(ct);
        await foreach (var trabalhoId in fila.LerTodosAsync(ct))
        {
            try
            {
                await ProcessarAsync(trabalhoId, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                log.LogError(ex, "Falha ao processar a importação {TrabalhoId}", trabalhoId);
                await MarcarFalhaAsync(trabalhoId, ct);
            }
        }
    }

    /// <summary>Recoloca na fila o que ficou pela metade quando o servidor reiniciou.</summary>
    private async Task RetomarInterrompidasAsync(CancellationToken ct)
    {
        try
        {
            await using var escopo = fabricaDeEscopos.CreateAsyncScope();
            var bd = escopo.ServiceProvider.GetRequiredService<ContextoImportacao>();
            var ids = await bd.Trabalhos
                .Where(t => t.Status == StatusDaImportacao.NaFila || t.Status == StatusDaImportacao.Processando)
                .Select(t => t.Id).ToListAsync(ct);
            foreach (var id in ids) await fila.EnfileirarAsync(id, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            log.LogWarning(ex, "Não foi possível retomar importações pendentes");
        }
    }

    private async Task ProcessarAsync(Guid trabalhoId, CancellationToken ct)
    {
        await using var escopo = fabricaDeEscopos.CreateAsyncScope();
        var bd = escopo.ServiceProvider.GetRequiredService<ContextoImportacao>();
        var paradas = escopo.ServiceProvider.GetRequiredService<IModuloParadas>();
        var notificador = escopo.ServiceProvider.GetRequiredService<INotificadorTempoReal>();

        var trabalho = await bd.Trabalhos.FirstOrDefaultAsync(t => t.Id == trabalhoId, ct);
        if (trabalho is null || trabalho.Status is StatusDaImportacao.Concluida or StatusDaImportacao.Falhou) return;

        trabalho.Iniciar();
        await bd.SaveChangesAsync(ct);
        var responsavel = new ResponsavelPelaParada(trabalho.UsuarioId, trabalho.OrganizacaoId);

        while (trabalho.LinhasPendentes.Count > 0)
        {
            var lote = trabalho.LinhasPendentes.Take(TamanhoDoLote).ToList();
            var validas = lote.Where(l => l.TemEndereco).ToList();
            var errosDoLote = lote.Where(l => !l.TemEndereco)
                .Select(l => new ErroDeLinha(l.NumeroDaLinha, "Linha sem endereço, CEP ou coordenadas.")).ToList();

            var resultado = await paradas.IncluirAsync(responsavel, trabalho.RotaId,
                validas.Select(ConversorDeLinhas.Converter).ToList(), OrigemDaParada.Planilha, ct: ct);

            if (resultado.Falha)
            {
                trabalho.Falhar(resultado.Erro!.Mensagem, relogio.GetUtcNow());
                await bd.SaveChangesAsync(ct);
                break;
            }

            var r = resultado.Valor;
            errosDoLote.AddRange(r.Erros.Select(e => new ErroDeLinha(validas[e.Indice].NumeroDaLinha, e.Mensagem)));
            trabalho.RegistrarLote(lote.Count, r.Criadas, r.PrecisamConfirmacao, r.SemLocalizacao, errosDoLote);
            await bd.SaveChangesAsync(ct);

            await notificador.NotificarUsuarioAsync(trabalho.UsuarioId, EventosTempoReal.ProgressoImportacao, new
            {
                trabalhoId = trabalho.Id,
                processadas = trabalho.LinhasProcessadas,
                total = trabalho.TotalDeLinhas,
                status = trabalho.Status.ToString()
            }, ct);
        }

        if (trabalho.Status == StatusDaImportacao.Processando)
        {
            trabalho.Concluir(relogio.GetUtcNow());
            await bd.SaveChangesAsync(ct);
        }

        await notificador.NotificarUsuarioAsync(trabalho.UsuarioId, EventosTempoReal.ProgressoImportacao, new
        {
            trabalhoId = trabalho.Id,
            processadas = trabalho.LinhasProcessadas,
            total = trabalho.TotalDeLinhas,
            status = trabalho.Status.ToString()
        }, ct);
    }

    private async Task MarcarFalhaAsync(Guid trabalhoId, CancellationToken ct)
    {
        await using var escopo = fabricaDeEscopos.CreateAsyncScope();
        var bd = escopo.ServiceProvider.GetRequiredService<ContextoImportacao>();
        var trabalho = await bd.Trabalhos.FirstOrDefaultAsync(t => t.Id == trabalhoId, ct);
        if (trabalho is null) return;
        trabalho.Falhar("Erro inesperado durante a importação.", relogio.GetUtcNow());
        await bd.SaveChangesAsync(ct);
    }
}
